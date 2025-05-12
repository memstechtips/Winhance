# Winhance - Windows Enhancement Utility 🚀

**Winhance** is a C# application designed to optimize and customize your Windows experience. From software management to system optimizations and customization, Winhance provides functions to enhance Windows 10 and 11 systems.

**Winhance** features most of the same enhancements as [UnattendedWinstall](https://github.com/memstechtips/UnattendedWinstall) without needing to do a clean install of Windows.

## Requirements 💻
- Windows 11
  - *Tested on Windows 11 24H2*
  - *Most things should work on Windows 10 22H2 but there are some issues like Microsoft Edge (legacy) removal*

## Installation 📥

Download from [winhance.net](https://winhance.net) or the [Releases](https://github.com/memstechtips/Winhance/releases) section of this repository.

The `Winhance.Installer.exe` includes an Installable and Portable version during setup.

## 🔐 Winhance v25.05.05 Security Info

**Important:** Please verify your download using the information below. Any file with different values for this particular version is not from the official source.

- **Winhance.Installer.exe**
  - Size: 111853604 bytes : 106 MiB
  - SHA256: 7089df9406023bd5a5a311d45c5a2fe861dd22190c0e8410477df3a94133026b

- **Winhance.exe**
  - Size: 155136 bytes : 151 KiB
  - SHA256: ba87eff9b7350c5499f2e28cf5f01c9b34d6a90829b6e5f27e8ef8cd7266f5df

> [!NOTE]
> This tool is currently in development. Any issues can be reported using the Issues tab.<br>
> Also, I'm not a developer, I'm just enjoying learning more about scripting/programming and learning as I go.<br><br>
> Please also understand that I prefer to develop and work on these projects independently.<br>I do value other people's insights and appreciate any feedback, but don't take it personally if a pull request is not accepted.

## Support the developer

It really does make a big difference, and is very much appreciated. Thanks<br>
[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/memstechtips)

## Current Features 🛠️

### Software & Apps 💿
- **Windows Apps & Features Section**
  - Searchable interface with explanatory legend
  - Organized sections for Windows Apps, Legacy Capabilities, and Optional Features
  - One-click removal and installation of selected items
- **External Apps Section**
  - Install various useful applications via WinGet
  - Categories include Browsers, Multimedia utilities, Document viewers, and more

### Optimize 🚀
- Searchable interface with status indicators
- Toggle switches for each setting for better control
- Set UAC Notification Level
- Privacy Settings
- Gaming and Performance Optimizations
- Windows Updates
- Explorer Optimizations
- Power Settings with Power Plan selection
- Sound Settings
- Notification Preferences

### Customize 🎨
- Searchable interface with status indicators
- Toggle switches for each setting for better control
- Windows Theme selector (Dark/Light Mode) via dropdown
- Taskbar Customization
- Start Menu Customization
- Explorer Customizations

### Other Settings 
- Manage Your Winhance (and Windows) Settings with Wihance Configuration Files:
   - Save settings currently applied in Winhance to a config file for easy importing on a new system or after a fresh Windows install.
- Toggle Winhance's theme (Light/Dark Mode)

## 🗺️ ROADMAP/TODO
The items below are planned changes and features for future releases.

🔜 = Coming Soon
⌛ = In Progress
✅ = Completed

Note: ✅ Completed items are commited to the source code files, but the changes will not be visible in the application until the next release or update is released to the application.

### Winhance Installation
⌛ Fix Winhance Shortcut not appearing in the Start Menu (All Programs)  
⌛ Fix the changing of default Installation location not accepted by Winhance installer. Issue #154  
⌛ Add a Winhance Winget package to make Winhance installable via WinGet. Issue #159  

### Main Window
⌛ Add a "More" Navigation button in the Main Window that when clicked, shows options for:  
  Winhance Version with check for updates button  
  About Winhance  
  Winhance Logs  
  Winhance Scripts  
  etc.  

#### Config Import
🔜 Improve Config Import to have checkboxes for sub sections in each category  

### Software & Apps Screen

🔜 Add detection of installed apps and update notifications for those apps  
⌛ Add an internet connection check before attempting to install an application using Winhance. Also add internet checks during app installation in case of timeouts occurring. Issue #155  
🔜 Add an option to enable and Activate Windows Photo Viewer. Issue #135  
✅ Fix the typo on the custom dialog box that incorrectly says "installd" and which should show "installed". Issue #146  

#### Windows Apps & Features
🔜 Add Icons next to the "Winhance Removal Status" that when clicked, deletes the scripts and scheduled tasks that are present when Winhance was previously run (ie. BloatRemoval.ps1, EdgeRemoval.ps1 and OneDriveRemoval.ps1)  
🔜 Rework EdgeRemoval script so it doesn't uninstall WebView. Also, update WebView installation  
🔜 Fix "We can't open this 'microsoft-edge' link" due to edge removal and no default browser found. Issue #38  
⌛ WinHance Fails to Remove OneNote on Windows 11. This is due to OneNote no longer being an appx-package (like on Windows 10) and must be uninstalled using it's uninstaller. Issue #141  

#### External Software
🔜 For app installations, give users the option to choose a location to install the application. Issue #160  
🔜 Add a "website" icon next to each app in external software that will take the user to the specific app's webpage so users can get more info about the app before installing it. Issue #152  
🔜 Status Feature for External Software: Similar to Windows software, add a status feature for external applications to indicate whether they are installed. If installed, show if updates are available (updates indicator for windows softwares as well). Issue #142  
🔜 Indicator for App Purchases: Include an indicator for apps to show if they are completely free, partially free/paid, and completely paid. Issue #142  
⌛ Downloading Adwcleaner or Malwarebytes not working. Issue #163  
🔜 Add ability to select the programs that users currently have installed on their computers to the external apps section and that they can be added to the config file. Issue #165  
⌛ Add KeepassXC to External Software (Privacy & Security Section). Issue #133  
⌛ Add PotPlayer and all apps from ninite.com to External Apps. Issue #138  
⌛ Add Wise Registry Cleaner to External Apps. Issue #164  
⌛ Add bcuninstaller to the external apps section. Issue #161  
⌛ Apps to add to the external software section if possible: Explorer Patcher, Classic Task Manager, Volume2, NirCmd, Crystal Disk Info, TailScale, TriggerCMD, SyncToy, Minimize to Tray, StreamLabs OBS, Input Leap, Bulk Rename Utility, Serial Port Notifier, Sereby AIO Runtime, AutoHotKey, FlowFrames, Text-Grab, VenToy, Unlocker. Issue #135  
⌛ Add Windhawk, Lively Wallpaper, Rainmeter.   

### Optimize Screen

#### Windows Security Settings
⌛ Improve UAC slider to match all of the available options in Windows. User Account Control seems to be automatically set to "High" if a user has selected a value higher than that in Windows. This is due to Winhance not having all the available UAC options. Issue #166  

#### Power Management
🔜 Improve the power section to detect all power plans on the pc and add indicators next to each power plan in the dropdown menu showing what currently exists  
🔜 Improve the power section to have toggles that toggle the powercfg commands on the currently applied power plan (currently, importing the Ultimate Performance powerplan just automatically applies all "recommended" powercfg commands but there is no control over it) This will also fix #84  

#### Gaming & Performance
🔜 Review the "Mouse" related settings and why Enhance mouse precision won't work anymore due to mouse related tweaks in the gaming section. Issue #13  
🔜 Consider adding preset options in optimizations tab like "Minimal, recommended and extreme optimizations" (This can also be done with preset config files if developed and shared.)  
⌛ Add Disable High Precision Event Timer under Gaming. Issue #139  
⌛ Fix Removing XBox makes the popup "Get an app to open this 'ms-gamingoverlay'" by adding the registry entry that fixes the popup from appearing. Issue #34, #123, #158  

#### Notifications
⌛ Add Toggles for "Location Notifications" and "Windows Security Notifications"  

#### Explorer Improvements
⌛ Add "Always show all system tray icons" registry entry (works on Windows 10 only). Issue #18  
🔜 Test if right clicking the quick access panel crashes explorer because of classic context menu implementation. Issue #24  
⌛ Add toggles to show hidden files, folders, drives and also uncheck the "Hide Protected Operating System Files" check. Issue #30  
⌛ Add toggle to Remove lock screen. Issue #105  
⌛ Add option to not compress wallpaper. Issue #130  
🔜 Toggle to remove the "open in terminal" option from right click context menu. Issue #162  
🔜 Add toggles to remove "-Shortcut" Text, Disable Always Ask Before Opening. Issue #135  

#### Windows Updates
✅ Change "Exclude Drivers from Windows Updates" toggle label to "Do not include drivers with Windows Updates" so it matches what Windows says in configured update policies. Issue #167  

### Other General Requests, Features & Issues
⌛ Improve scrolling speed and make it faster in all views.  
🔜 Debloat ink handwriting main store. Issue #65  
🔜 Add old F8 Menu to startup screen  
🔜 Can't type in Start Menu search bar. (This is due to tweaks included in the first version of Winhance, need to investigate). Issue #25  
🔜 Add translations for different languages (not currently a top priority). Issue #51  
🔜 Posting Template & Wiki. Issue #91  
🔜 Disable windows search indexing tool. Issue #74  

#### UI Improvements
⌛ Clearer indication between toggle Enabled & Disabled states  
🔜 Update tooltips to be more descriptive about what a toggle does  


## Feedback and Community

If you have feedback, suggestions, or need help with Winhance, please join the discussion on GitHub or our Discord community:

[![Join the Discussion](https://img.shields.io/badge/Join-the%20Discussion-2D9F2D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/memstechtips/Winhance/discussions)
[![Join Discord Community](https://img.shields.io/badge/Join-Discord%20Community-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://www.discord.gg/zWGANV8QAX)