using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using zlib;

namespace Assembly_CSharp.TasInfo.mm.Source {
    internal static class ReplayExportCodec {
        public const float RecordFps = 30f;
        public const float RecordInterval = 1f / RecordFps;
        private const float PosScale = 100f;

        private static readonly byte[] RoomMagic = {
            (byte)'R', (byte)'T', (byte)'M', (byte)'3'
        };

        private static readonly byte[] CollectionMagic = {
            (byte)'R', (byte)'T', (byte)'M', (byte)'C'
        };

        private const byte RoomVersion = 0x02;
        private const byte CollectionVersion = 0x01;

        public static string EncodeRoom(ReplayExportRoom room) {
            byte[] binary = WriteRoomBinary(room);
            return Convert.ToBase64String(CompressRaw(binary));
        }

        public static string EncodeCollection(IList<ReplayExportRoom> rooms) {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8)) {
                writer.Write(CollectionMagic);
                writer.Write(CollectionVersion);
                writer.Write(rooms.Count);
                foreach (ReplayExportRoom room in rooms) {
                    byte[] blob = WriteRoomBinary(room);
                    writer.Write(blob.Length);
                    writer.Write(blob);
                }

                writer.Flush();
                return Convert.ToBase64String(CompressRaw(ms.ToArray()));
            }
        }

        public static string SerializeSceneJson(IList<ReplayExportRoom> rooms) {
            var sb = new StringBuilder();
            sb.Append("{\"entries\":[");
            long capturedAtUtcTicks = DateTime.UtcNow.Ticks;

            for (int i = 0; i < rooms.Count; i++) {
                if (i > 0) {
                    sb.Append(',');
                }

                ReplayExportRoom room = rooms[i];
                sb.Append("{\"snapshotId\":");
                AppendJsonString(sb, CreateSnapshotId(room, capturedAtUtcTicks, i));
                sb.Append(",\"capturedAtUtcTicks\":");
                sb.Append(capturedAtUtcTicks.ToString(CultureInfo.InvariantCulture));
                sb.Append(",\"sceneName\":");
                AppendJsonString(sb, room.Key.SceneName);
                sb.Append(",\"entryFromScene\":");
                AppendJsonString(sb, room.Key.EntryFromScene);
                sb.Append(",\"exitToScene\":");
                AppendJsonString(sb, room.Key.ExitToScene);
                sb.Append(",\"totalTime\":");
                sb.Append(room.TotalTime.ToString("R", CultureInfo.InvariantCulture));
                sb.Append(",\"data\":");
                AppendJsonString(sb, EncodeRoom(room));
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static string CreateSnapshotId(ReplayExportRoom room, long capturedAtUtcTicks, int index) {
            string firstFrame = room.FrameCount > 0
                ? room.Frames[0].x.ToString("R", CultureInfo.InvariantCulture) + "," +
                  room.Frames[0].y.ToString("R", CultureInfo.InvariantCulture)
                : string.Empty;
            string lastFrame = room.FrameCount > 0
                ? room.Frames[room.FrameCount - 1].x.ToString("R", CultureInfo.InvariantCulture) + "," +
                  room.Frames[room.FrameCount - 1].y.ToString("R", CultureInfo.InvariantCulture)
                : string.Empty;
            string seed = string.Join("|", new[] {
                capturedAtUtcTicks.ToString(CultureInfo.InvariantCulture),
                index.ToString(CultureInfo.InvariantCulture),
                room.Key.SceneName ?? string.Empty,
                room.Key.EntryFromScene ?? string.Empty,
                room.Key.ExitToScene ?? string.Empty,
                room.TotalTime.ToString("R", CultureInfo.InvariantCulture),
                room.FrameCount.ToString(CultureInfo.InvariantCulture),
                firstFrame,
                lastFrame
            });

            using (var md5 = MD5.Create()) {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(seed));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        private static byte[] WriteRoomBinary(ReplayExportRoom room) {
            int frameCount = room.FrameCount;
            byte[] xStream = Encode2ndOrder(room.Frames, true);
            byte[] yStream = Encode2ndOrder(room.Frames, false);
            int facingBytes = (frameCount + 7) / 8;
            var clipTable = new List<string>();
            var clipIndexes = new byte[frameCount];
            var animFrames = new byte[frameCount];
            bool hasAnimation = false;

            for (int i = 0; i < frameCount; i++) {
                ReplayExportFrame frame = room.Frames[i];
                string clip = frame.animClip ?? string.Empty;
                if (clip.Length == 0) {
                    clipIndexes[i] = 0xFF;
                } else {
                    hasAnimation = true;
                    int clipIndex = clipTable.IndexOf(clip);
                    if (clipIndex < 0) {
                        clipIndex = clipTable.Count;
                        clipTable.Add(clip);
                    }

                    clipIndexes[i] = (byte)Math.Min(clipIndex, 254);
                }

                animFrames[i] = (byte)Math.Min(frame.animFrame, 255);
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.UTF8)) {
                writer.Write(RoomMagic);
                writer.Write(RoomVersion);
                WriteString(writer, room.Key.SceneName);
                WriteString(writer, room.Key.EntryFromScene);
                WriteString(writer, room.Key.ExitToScene);
                writer.Write(room.TotalTime);
                writer.Write(frameCount);
                writer.Write((ushort)xStream.Length);
                writer.Write(xStream);
                writer.Write((ushort)yStream.Length);
                writer.Write(yStream);

                for (int byteIndex = 0; byteIndex < facingBytes; byteIndex++) {
                    byte bits = 0;
                    for (int bit = 0; bit < 8; bit++) {
                        int frameIndex = byteIndex * 8 + bit;
                        if (frameIndex < frameCount && room.Frames[frameIndex].facingRight) {
                            bits |= (byte)(0x80 >> bit);
                        }
                    }
                    writer.Write(bits);
                }

                byte clipCount = (byte)(hasAnimation ? clipTable.Count : 0);
                writer.Write(clipCount);
                if (clipCount > 0) {
                    foreach (string clipName in clipTable) {
                        WriteString(writer, clipName);
                    }

                    writer.Write(clipIndexes);
                    writer.Write(animFrames);
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        private static byte[] Encode2ndOrder(ReplayExportFrame[] frames, bool getX) {
            int count = frames.Length;
            if (count == 0) {
                return new byte[0];
            }

            using (var ms = new MemoryStream(2 + count * 3))
            using (var writer = new BinaryWriter(ms, Encoding.UTF8)) {
                short first = ToShort(getX ? frames[0].x : frames[0].y);
                writer.Write(first);
                if (count == 1) {
                    writer.Flush();
                    return ms.ToArray();
                }

                short second = ToShort(getX ? frames[1].x : frames[1].y);
                WriteSvlq(writer, second - first);

                short prev2 = first;
                short prev1 = second;
                for (int i = 2; i < count; i++) {
                    short current = ToShort(getX ? frames[i].x : frames[i].y);
                    WriteSvlq(writer, current - 2 * prev1 + prev2);
                    prev2 = prev1;
                    prev1 = current;
                }

                writer.Flush();
                return ms.ToArray();
            }
        }

        private static void WriteSvlq(BinaryWriter writer, int value) {
            uint encoded = value >= 0 ? (uint)(value << 1) : (uint)((-value << 1) - 1);
            do {
                byte chunk = (byte)(encoded & 0x7F);
                encoded >>= 7;
                if (encoded != 0) {
                    chunk |= 0x80;
                }
                writer.Write(chunk);
            } while (encoded != 0);
        }

        private static void WriteString(BinaryWriter writer, string value) {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static short ToShort(float value) {
            int scaled = (int)Math.Round(value * PosScale);
            if (scaled < short.MinValue) {
                scaled = short.MinValue;
            } else if (scaled > short.MaxValue) {
                scaled = short.MaxValue;
            }
            return (short)scaled;
        }

        private static byte[] CompressRaw(byte[] input) {
            var stream = new ZStream();
            int init = stream.deflateInit(zlibConst.Z_BEST_COMPRESSION, -15);
            if (init != zlibConst.Z_OK) {
                throw new InvalidOperationException("raw deflate init failed: " + init);
            }

            try {
                stream.next_in = input;
                stream.next_in_index = 0;
                stream.avail_in = input.Length;

                byte[] buffer = new byte[4096];
                using (var output = new MemoryStream()) {
                    while (true) {
                        stream.next_out = buffer;
                        stream.next_out_index = 0;
                        stream.avail_out = buffer.Length;

                        int flush = stream.avail_in > 0 ? zlibConst.Z_NO_FLUSH : zlibConst.Z_FINISH;
                        int err = stream.deflate(flush);
                        if (err != zlibConst.Z_OK && err != zlibConst.Z_STREAM_END) {
                            throw new InvalidOperationException("raw deflate failed: " + err + " " + stream.msg);
                        }

                        int written = buffer.Length - stream.avail_out;
                        if (written > 0) {
                            output.Write(buffer, 0, written);
                        }

                        if (err == zlibConst.Z_STREAM_END) {
                            return output.ToArray();
                        }
                    }
                }
            } finally {
                stream.deflateEnd();
            }
        }

        private static void AppendJsonString(StringBuilder sb, string value) {
            sb.Append('"');
            foreach (char c in value ?? string.Empty) {
                switch (c) {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20) {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
