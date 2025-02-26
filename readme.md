# Reminder

The purpose of this tool is to remind some topics we feel important by poping up urls at a regular period.

You can use it to popup daily a energizing sentence, to remind you the goal of the month, to remind you good practice ... 

## How to use?
Just start the application clicking on 'Reminder.exe', a new icon will appears in the systray. If you already configured some pages, they will be opened at the right time.

You can add, remove, update pages directly in the configuration file, which is accessible from the context menu in the systray app.
The configuration will be reloaded automatically as soon you save it. 

The application execute the configuration every minute.

The application will automatically register itself to start at windows startup.

## Example of configuration file

To help you, a schema is provided in the repository. You can use it in your editor to have a better experience.

You have to specify the url of the page you want to open, the frequency of the reminder. The next opening date will be automatically calculated after the 1st opening. Anyway, it could be manually update to fit your needs in term of date and time.

```yaml
# yaml-language-server: $schema=./configSchema.json
pages:
  - url: https://www.example.com
    frequency: daily
    nextOpening: 2025-02-26T15:12:18Z
    
  # - url: onenote:https://siemens-my.sharepoint.com/personal/richard_lasjunies_siemens_com/Documents/Notebooks/Journal2025/Feb-25.one#2025%2002%2026&section-id={0AB20093-17D3-431C-A997-A28DA6B647D0}&page-id={B241A038-1642-4224-9397-42D0578A9F6A}&end
  #   frequency: weekly
  #   nextOpening: 2025-02-26T15:21:00Z
   
  - url: https://onedrive.live.com/redir?resid=3981CA5E56F3AD91%21387606&page=Edit&wd=target%28Reminder%20-%20strategy%20box.one%7Cff8cfb6b-8db5-426c-a9a2-10ed6d2c88c3%2FMonthly%20goals%7C5a2559be-5300-4193-82a0-13fa873d91ae%2F%29&wdorigin=703
    frequency: weekly
    
  # - url: https://www.example4.com
  #   frequency: quarterly
```

