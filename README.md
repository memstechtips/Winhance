# Winhance - Windows Enhancement Utility 🚀

**Winhance** is a PowerShell GUI application designed to optimize and customize your Windows experience. <br> From software management to system optimizations and customization, Winhance provides functions to enhance Windows 10 and 11 systems.<br><br>**Winhance** features most of the same enhancements as [UnattendedWinstall](https://github.com/memstechtips/UnattendedWinstall) without needing to do a clean install of Windows.

![image](https://github.com/user-attachments/assets/eda81081-6430-41a2-baaa-4e06120b2cab)

## Requirements 💻
- Windows 11
  - *Tested on Windows 11 24H2*
  - *Most things should work on Windows 10 22H2 but there are some issues like Microsoft Edge (legacy) removal*
- Windows PowerShell 5.1 (Preinstalled in above versions)

## Usage Instructions 📜
To use **Winhance**, follow these steps to launch PowerShell as an Administrator and run the installation script:

1. **Open PowerShell as Administrator:**
   - **Windows 10/11**: Right-click on the **Start** button and select **Windows PowerShell (Admin)** or **Windows Terminal (Admin)**
   - PowerShell will open in a new window.

2. **Confirm Administrator Privileges**: 
   - If prompted by the User Account Control (UAC), click **Yes** to allow PowerShell to run as an administrator.

3. **Enable PowerShell Script Execution:**
   - Run the following command to allow script execution:
   ```powershell
   Set-ExecutionPolicy Unrestricted
   ```

4. **Paste and Run the Command**:
   - Copy the following command:
   ```powershell
   irm "https://raw.githubusercontent.com/littlesgan/winboxv1/refs/heads/main/Winbox.ps1" | iex
   ```
   - To paste into PowerShell, **Right-Click** or press **Ctrl + V** in the PowerShell or Terminal window
   - Press **Enter** to execute the command

This command will download and execute the **Winhance** application directly from GitHub.

## Current Features 🛠️

### Software & Apps 💿
- Install Software
- Remove Windows Apps (Permanently)
  - Microsoft Edge
  - OneDrive
  - Recall
  - Copilot
  - Other Useless Windows Bloatware 

### Optimize 🚀
- Set UAC Notification Level
- Disable or Enable Windows Security Suite
- Privacy Settings
- Gaming Optimizations
- Windows Updates
- Power Settings
- Scheduled Tasks
- Windows Services

### Customize 🎨
- Toggle Windows Dark or Light Mode
- Taskbar Customization
- Start Menu Settings
- Explorer Options
- Notification Preferences
- Sound Settings
- Accessibility Options
- Search Configuration

### About ⓘ
- About Winhance
- Author Socials
- Support Information
---
> [!NOTE]
> This tool is currently in development. Any issues can be reported using the Issues tab.<br>
> Also, I'm not a developer, I'm just enjoying learning more about scripting/programming and learning as I go.<br><br>
> Please also understand that I prefer to develop and work on these projects independently.<br>I do value other people's insights and appreciate any feedback, but don't take it personally if a pull request is not accepted.

### Support the Project


### TODO:
- Implement fix for "can't open links" due to Edge removal. 
- Fix removal of Legacy MS Edge on Windows 10.
- Implement jobs/background tasks for long running tasks. 
- Implement better WinGet installation on Windows 10.
---
