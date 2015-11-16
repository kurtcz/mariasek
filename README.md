# mariasek

Requirements
------------
[Microsoft .NET Framework 4.5]( https://www.microsoft.com/en-us/download/details.aspx?id=30653 | )

Configuration
-------------

Most of the configuration can be done in a settings window. Few options need to be edited in a config file.
The configuration file is `Mariasek.TesterGUI.exe.config`
**Email settings:**
```
  <system.net>
    <mailSettings>
  	  <smtp deliveryMethod="network" from="user@domain.com">
        <network
          host="smtp.gmail.com"
          port="587"
          defaultCredentials="false"
          enableSsl="true"
        />
      </smtp>
  </mailSettings>
  </system.net>
```
The SMTP username and password can be set from the settings window.