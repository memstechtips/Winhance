# Winhance - Windows Enhancement Utility 🚀

**Winhance** is a PowerShell GUI application designed to optimize and customize your Windows experience. <br> From software management to system optimizations and customization, Winhance provides functions to enhance Windows 10 and 11 systems.<br><br>**Winhance** features most of the same enhancements as [UnattendedWinstall](https://github.com/memstechtips/UnattendedWinstall) without needing to do a clean install of Windows.

![image](https://github.com/user-attachments/assets/01b70777-f384-4ba4-8fc1-7dca81250f5a)

## Requirements 💻
- Windows 11
  - *Tested on Windows 11 24H2*
  - *Most things should work on Windows 10 22H2 but there are some issues*
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
   irm "https://github.com/memstechtips/Winhance/raw/main/Winhance.ps1" | iex
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

If **Winhance** has been useful to you, consider supporting the project—it truly helps!

[![Support via PayPal](https://img.shields.io/badge/Support-via%20PayPal-FFD700?style=for-the-badge&logo=paypal&logoColor=white)](https://paypal.me/memstech)

### Feedback and Community

If you have feedback, suggestions, or need help with Winhance, please join the discussion on GitHub or our Discord community:

[![Join the Discussion](https://img.shields.io/badge/Join-the%20Discussion-2D9F2D?style=for-the-badge&logo=github&logoColor=white)](https://github.com/memstechtips/Winhance/discussions)
[![Join Discord Community](https://img.shields.io/badge/Join-Discord%20Community-5865F2?style=for-the-badge&logo=discord&logoColor=white)](https://www.discord.gg/zWGANV8QAX)

---
