# Hollow Knight libTAS Setup Guide

This guide assumes you already have:

- Ubuntu 25.10 installed.
- Steam installed.
- Hollow Knight downloaded in Steam.
- The files I sent you copied into your home folder.

Your home folder is usually:

```text
/home/YOUR_USERNAME
```

If your username is `hk-tas`, then your home folder is:

```text
/home/hk-tas
```

## Using the File Explorer

Ubuntu's file explorer is called `Files`.

Some folders you need are hidden because their names start with a dot, like `.config` and `.local`.

To show hidden folders:

1. Open `Files`.
2. Go to your home folder.
3. Press `Ctrl + H`.

Press `Ctrl + H` again to hide them later.

You can do many steps in this guide either with Terminal commands or by copying files in `Files`. If a folder path starts with `$HOME`, that means your home folder.

## 1. Make Sure Hollow Knight Is the Right Version

For this setup, Hollow Knight needs to be the native Linux version of patch `1.4.3.2`, also called `1432`.

If you already downpatched to `1432`, good. Still check that Steam is using the Linux version, not Proton.

### Switch Hollow Knight to the Linux Version

1. Open Steam.
2. Go to `Library`.
3. Right-click `Hollow Knight`.
4. Click `Properties`.
5. Click `Compatibility`.
6. If `Force the use of a specific Steam Play compatibility tool` is checked, uncheck it.
7. If you must choose a tool, choose `Steam Linux Runtime 1.0 (scout)`, not Proton.
8. Close the Properties window.
9. Click the gear/manage button for Hollow Knight.
10. Click `Installed Files`.
11. Click `Verify integrity of game files`.

After this, Hollow Knight should have this file inside its install folder:

```text
hollow_knight.x86_64
```

For the Snap version of Steam, the install folder is usually:

```text
$HOME/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/
```

So the full game file path is usually:

```text
$HOME/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight.x86_64
```

That is the Linux game file. If you only see `hollow_knight.exe`, Steam installed the Windows/Proton version, which is not what we want.

## 2. Open Hollow Knight Once in Steam

Before using libTAS, launch Hollow Knight normally from Steam one time.

Do this setup in the game:

- Pick your language.
- Set particle effects to low.
- Turn the frame cap off.
- If you are doing runs with dialogue, use Chinese because it is fastest. In the language menu, Chinese should be two clicks left of English.
- Close the game after you reach the main menu.

This step makes sure the game creates its normal settings and save folders.

## 3. Install the TAS Info Tool

Open Terminal.

If you do not know how:

1. Press the `Windows` key or click `Activities`.
2. Type `Terminal`.
3. Press `Enter`.

Now copy and paste this command:

```bash
cd "$HOME" && mkdir -p tasInfo && cd tasInfo && unzip -o "$HOME/HK_TAS_Info_Tool_v0.2.5.zip" && ./install-linux.sh
```

The script should find your Hollow Knight install.

It should say it found target `v1432`.

When it asks if you want to install, type:

```text
y
```

Then press `Enter`.

If the script says `No files were installed`, read the error above that line. Usually it means one of these:

- Hollow Knight is not `1432`.
- Steam installed the Proton/Windows version instead of the Linux version.
- The zip file was not extracted correctly.
- The script could not find Hollow Knight automatically.

## 4. Copy the libTAS Settings I Sent You

Only do this if you are okay replacing your current libTAS settings.

### Option A: Use Files

1. Open `Files`.
2. Go to your home folder.
3. Press `Ctrl + H` so you can see hidden folders.
4. Open `.config`.
5. If there is no `libTAS` folder, create one.
6. Open the `libTASConfigs` folder I sent you.
7. Copy everything inside `libTASConfigs`.
8. Paste it into `.config/libTAS`.

### Option B: Use Terminal

Run:

```bash
mkdir -p "$HOME/.config/libTAS"
cp "$HOME/libTASConfigs/"* "$HOME/.config/libTAS/"
```

This copies my working libTAS settings into your libTAS config folder.

## 5. Unzip the TAS Movie Files

If I sent you `wp_tas.zip`, unzip it like this:

```bash
cd "$HOME"
unzip -o wp_tas.zip
```

The `-o` means overwrite old files if they already exist.

For the White Palace TAS example, the zip should include:

- A movie/input file for libTAS.
- A Hollow Knight save file ending in `.dat`.

That example expects the save to be in save slot 1. Save slot 1 means the file must be named:

```text
user1.dat
```

If the `.dat` file in the zip has a different name, rename it to `user1.dat` before putting it in the Hollow Knight save folder.

## 6. Start libTAS

In Terminal, run:

```bash
libTAS
```

If that does not work, tell me the exact error message.

## 7. Pick the Hollow Knight Game File in libTAS

In libTAS, find the game executable dropdown near the top.

Choose:

```text
hollow_knight.x86_64
```

If the path is wrong, fix it.

For example, if the path says:

```text
/home/taser/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight.x86_64
```

but your username is `hk-tas`, change it to:

```text
/home/hk-tas/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight.x86_64
```

For the Snap version of Steam, Hollow Knight is usually here:

```text
$HOME/snap/steam/common/.local/share/Steam/steamapps/common/Hollow Knight/hollow_knight.x86_64
```

## 8. Set the Game Resolution

The command-line options in libTAS can force the game resolution.

I usually use:

```text
-screen-width 1280 -screen-height 720 -screen-fullscreen 0
```

If the game is too slow while fast-forwarding, try a smaller size:

```text
-screen-width 640 -screen-height 480 -screen-fullscreen 0
```

Smaller resolution is usually faster for TASing and video encoding.

## 9. Hollow Knight Save Files

On Ubuntu with native Linux Steam, Hollow Knight saves are usually here:

```text
$HOME/.config/unity3d/Team Cherry/Hollow Knight/
```

The save slots are files:

```text
user1.dat
user2.dat
user3.dat
user4.dat
```

They match the in-game save slots:

- `user1.dat` is save slot 1.
- `user2.dat` is save slot 2.
- `user3.dat` is save slot 3.
- `user4.dat` is save slot 4.

The White Palace TAS example expects save slot 1, so its save file needs to be `user1.dat`.

## 10. Copy a Save File to Another Slot

Close Hollow Knight before copying saves.

### Option A: Use Files

1. Open `Files`.
2. Go to your home folder.
3. Press `Ctrl + H` so you can see hidden folders.
4. Open `.config`.
5. Open `unity3d`.
6. Open `Team Cherry`.
7. Open `Hollow Knight`.

This is your save folder.

Before changing saves, copy your current `user1.dat`, `user2.dat`, `user3.dat`, and so on into a backup folder somewhere safe.

To copy a save into a slot:

1. Rename the save file to the slot you want, like `user1.dat` or `user3.dat`.
2. Copy it into the `Hollow Knight` save folder.
3. If Files asks whether to replace the old file, only click replace if you are sure.

### Option B: Use Terminal

To go to the save folder:

```bash
cd "$HOME/.config/unity3d/Team Cherry/Hollow Knight/"
```

To back up all your saves first:

```bash
mkdir -p "$HOME/HK-save-backups"
cp user*.dat "$HOME/HK-save-backups/"
```

Example: copy slot 1 into slot 2:

```bash
cp user1.dat user2.dat
```

Example: copy a save file I sent you into slot 1:

```bash
cp "$HOME/user1.dat" "$HOME/.config/unity3d/Team Cherry/Hollow Knight/user1.dat"
```

If I send you a file with a different name, like `lifeblood.dat`, and you want it in slot 3:

```bash
cp "$HOME/lifeblood.dat" "$HOME/.config/unity3d/Team Cherry/Hollow Knight/user3.dat"
```

Start Hollow Knight after copying the file. The save should appear in that slot.

## 11. If Something Goes Wrong

Do not guess. Copy the exact error text and send it to me.

Useful things to send:

- A screenshot of libTAS.
- The command you ran.
- The full Terminal error message.
- Whether Hollow Knight starts normally from Steam.
- Whether the game file is `hollow_knight.x86_64` or `hollow_knight.exe`.

## Quick Checklist

Before TASing, you want all of these to be true:

- Hollow Knight is patch `1432`.
- Hollow Knight is the native Linux version.
- The game file is `hollow_knight.x86_64`.
- Hollow Knight has been opened once normally through Steam.
- The TAS Info Tool installer finished successfully.
- libTAS is pointed at the correct `hollow_knight.x86_64`.
- libTAS has the command-line resolution options set.
- Your save file is in the correct `userN.dat` slot.
