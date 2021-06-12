# FileDaddy
<img src="https://media.discordapp.net/attachments/788721552392060990/831331891604095086/File_me_Daddy.png?width=604&height=609" width="250">

## Preface
This mod loader/manager is meant to simplify downloading and combining many different mods.

## Before Starting
If your game is currently modded, please restore the game back to its original state. You can do this by manually deleting your modded files and replacing them back with the original ones. Or you can just download the game again to start off from scratch. This is important since FileDaddy uses your current assets folder of the game as its base without mods.

## Getting Started
### Unzipping
Please unzip FileDaddy.zip before use. The application will not work otherwise. If you don't know how, simply Right Click > Extract All... then click Extract.
Make sure its extracted to a location NOT synced with the cloud such as OneDrive, Google Drive, etc.
### Prerequisites
In order to get FileDaddy to open up and run, you'll need to install some prerequisites.

When you open FileDaddy.exe for the first time this window might appear.

![Error Message](https://media.discordapp.net/attachments/750914797838794813/827798772184121374/one_time_i_saw_my_dad.png)

This means you need to install some prerequisites. When clicking yes, it should take you to this page as shown below.

![Prerequisite Page](https://media.discordapp.net/attachments/750914797838794813/827646015640436771/unknown.png?width=1147&height=609)

Click the Download x64 that I highlighted in red as shown above. An installer will be downloaded shortly after. Open it then click Install. After it finished installing, you should now be able to open up FileDaddy.exe without any errors. However, if you still run into errors, your operating system might be 32-bit so download and install x86 as well.

Alternatively, you can just download the installer from [here](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-5.0.4-windows-x64-installer) and run it before opening FileDaddy.exe and seeing the error message.

### Config
When finally opening FileDaddy.exe, first thing you want to do is click the Config button and set your Game Path to wherever your Friday Night Funkin executable is located. If you pick an executable that doesn't have an assets folder in the same directory, the console will spit out an error and tell you to try again.

As of v1.2.0, you can add as many paths to different executables as you want by choosing a proper Game Path under the Add New Game Path... selection. You can then swap between whichever Game Path you'd like to choose

### Installing Mods
You can get started with installing compatible mods by looking for this button ![button](https://media.discordapp.net/attachments/792245872259235850/827791904254066688/unknown.png) when exploring [Friday Night Funkin's GameBanana page](https://gamebanana.com/games/8694).

Note that this button will only work after you opened up the FileDaddy.exe for the first time.

Once you click it, a confirmation will appear on your browser asking if you want to open the application. Hit yes. If you don't want to continue seeing this confirmation just check off the Don't Ask Me From This Website before hitting yes.

A window will then open up confirming if you want to download the mod from FileDaddy.exe itself. Hit yes and a progress bar will let you know when the download is complete. Afterwards, the mod should appear on your mod list if it was successful. The name it appears under at the moment is the title of the GameBanana page. You can rename it if you'd like by right-clicking.

If you want to manually install mods, click the Open Mods button on top and drag a mod folder (not zip) into the directory.

### Building Your Loadout
After installing the mods you want, you can reorganize the priority of the list by simply dragging and dropping the rows in your desired position. You can also click the checkboxes to the left of the mods to enable/disable them. Mods higher up on the list will have higher priority. This means that if more than one mod modifies the same file, the highest mod's file will be the one used. 

Once you got your loadout setup, simply click the Build button and wait for it to notify you that it is finished building. After that, you can finally press the Launch button to start the game with your newly built loadout!

### Updating FileDaddy
As of v1.2.0, FileDaddy will check for updates for itself from GameBanana and prompt you to install it if it exists. This check will only happen if FileDaddy isn't launched by clicking 1-click Install from a mod page. It will then download, extract, replace, and restart after confirmation.

If any error with the auto updates were to occur please report it to me. You can then update manually by downloading the latest release and drag, drop, and overwrite its extracted contents over your current installation.

### Updating Mods
As of v1.4.0, you can now check for updates by clicking the Check for Updates button. When clicking the button for the first time on old mods, it'll add a last update time to the metadata no matter if its actually up to date or not. Any mods installed with 1-click post v1.4.0 will already have that part of the metadata. Updates are then found if it finds an update that is dated after the last update time in the metadata.

If an update is available, a window will prompt you with its description and changelog and asks if you'd like to update. Yes will update it and delete everything in the current mod folder and replace it with the update. No will not do anything. Skip Update will stop prompting the update until a new one is available.

If there's more than one file available to download, a prompt will open to select one of them. There'll be a short description, name of the zip, and how long ago it was uploaded.

## Folder Structure
As of v1.1.0, folder structure no longer matters! FileDaddy will just look through the entire assets folder until it finds the matching file name instead of relying on the folder path. Do note that if the mod download has multiple variations in the same folder, that it will go by alphanumeric order by folder names. So please keep variations separated.

Note: If you’re a mod maker and for some reason want to prevent 1-click install buttons from showing up you can just add an empty file inside the zip called .disable_gb1click_filedaddy

## Metadata
As of v1.3.0, 1-click installing will also fetch metadata stored in mod.json to be shown to the right of the grid. To fetch metadata for mods downloaded before the update, just right click the row, click Fetch Metadata, and enter the link to its page on GameBanana.

## Extra Options
There are some options that you can access by right clicking a mod row in the list:
- Fetch Metadata - Allows you to enter a link to the GameBanana page of the mod to fetch metadata to display on the right
- Rename Mod - Allows you to rename how the mod's name. Simply renames folder until I implement metadata. Note that at the current implementation it will remove the mod from the list and readd it under the new name all the way at the bottom disabled.
- Open Mod Folder - Opens the mod's folder in file explorer
- Delete Mod - Deletes mod folder after confirmation

## Mod Loading Process
1. FileDaddy will first look through all the files in the assets folder with the .backup extension. It will restore all those backups by overwriting the modified files with them.
2. Next, FileDaddy will go in order from lowest to highest priority and copy the mods' assets files over to the matching assets location in the game's files. The original files will be renamed to have a .backup extension to be restored in step 1 when you build again. If the mod's assets path for a file doesn't exist in the game's files, it won't be copied over.

## Issues/Suggestions
If you have any issues with FileDaddy please fill out an issue on this GitHub page. Suggestions are welcomed as well. There's probably some things I haven't considered since I personally don't play/mod Friday Night Funkin frequently.

## FAQ
### Is this safe? My antivirus is getting set off.
Yes this application is safe. Antivirus tends to trigger false alarms, especially due to it needing to be connected to the internet in order to be compatible with 1-click installations. You can check out the source code for yourself if your suspicious of anything as well.

### Why does this mod not have a 1-click install button?
Any mods with executables should not have a 1-click installation button appear. If it somehow does, do note that the executable that comes with it will not be handled by FileDaddy.

Instead, download these executable mods manually and set them in a location not inside FileDaddy's folder. Then add the executable path to the config to use as a base.

### Why won’t FileDaddy open?
I made it so only one instance is running at a time so if it’s already running, the app won’t open. Check to see if you can end the process in task manager or even restart your pc if you don’t know how to do that. 

### Why doesn't FileDaddy have permissions to copy over files?
Try running as administrator or checking to see if any antivirus is preventing the application from operating on files.

### Why aren't my mods showing up after checking them?
Please make sure you pressed build after selecting your loadout.

## Future Plans
None at the moment but I'm open to suggestions and pull requests.
