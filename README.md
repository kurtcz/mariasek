# mariasek

Requirements
------------
[Microsoft .NET Framework 4.5]( https://www.microsoft.com/en-us/download/details.aspx?id=30653 | )

Configuration
-------------

Most of the configuration can be done in a settings window. Few options need to be edited in a config file.
The configuration file is `Mariasek.TesterGUI.exe.config` and `Mariasek.Console.exe.config`

**AI settings**
To adjust AI settings differently for each AI player you need to adjust the config files directly.

```
    <player1 name="Alpha" type="Mariasek.Engine.New.AiPlayer" assembly="Mariasek.Engine.New.dll">
      <parameter name="AiCheating" value="false" />
      <parameter name="RoundsToCompute" value="1" />
      <parameter name="CardSelectionStrategy" value="MaxCount" />
      <parameter name="SimulationsPerRound" value="50" />
      <parameter name="RuleThreshold" value="70" />
      <parameter name="GameThreshold" value="55|65|75|85|95" />
      <parameter name="MaxDoubleCount" value="5" />
    </player1>
    <player2 name="Bravo" type="Mariasek.Engine.New.AiPlayer" assembly="Mariasek.Engine.New.dll">
      <parameter name="AiCheating" value="false" />
      <parameter name="RoundsToCompute" value="1" />
      <parameter name="CardSelectionStrategy" value="MaxCount" />
      <parameter name="SimulationsPerRound" value="50" />
      <parameter name="RuleThreshold" value="80" />
      <parameter name="GameThreshold" value="55|65|75|85|95" />
      <parameter name="MaxDoubleCount" value="5" />
    </player2>
    <player3 name="Charlie" type="Mariasek.Engine.New.AiPlayer" assembly="Mariasek.Engine.New.dll">
      <parameter name="AiCheating" value="false" />
      <parameter name="RoundsToCompute" value="1" />
      <parameter name="CardSelectionStrategy" value="MaxCount" />
      <parameter name="SimulationsPerRound" value="50" />
      <parameter name="RuleThreshold" value="90" />
      <parameter name="GameThreshold" value="55|65|75|85|95" />
      <parameter name="MaxDoubleCount" value="5" />
    </player3>
```

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

Batch Run Analysis
------------------

To test different AI settings there are a few useful scripts.

**Marisek.GameGerator**
This script generates a number of random games and saves their definitions to a chosen directory.

```
C:\Mariasek> run.cmd .\Mariasek.GameGenerator.ps1 -GamesToGenerate 100 -OutputDir GameDefinitions
```

**Mariasek.ConfigGenerator**
In order to test which of the given three AI configuration settings A, B and C is better we need to test each in every different combination of players.
This script generates config files with all possible combinations of players. For three players there is a total of 3! = 6 combinations: abc, acb, bac, bca, cab, cba

```
C:\Mariasek> run.cmd .\Mariasek.ConfigGenerator.ps1 -OutputDir GonfigFiles
```

**Mariasek.BatchRunner**
This tool is used to run simulations of a set of games over a set of configs. This is how to run a batch of games and save the report in files reportxyz.json and cfgxyz.json

```
C:\Mariasek> run.cmd .\Mariasek.BatchRunner.ps1 -InputDir GameDefinitions -OutputDir GameResults -ConfigDir ConfigFiles -ReportDir Reports -Scenario xyz
```

**Mariasek.ReportGenerator**
This script is being called from the previous one. Should you want to create a report in a different format you can run the tool directly as follows.
Supported formats are JSON, XML can CSV. 
```
C:\Mariasek> run.cmd .\Mariasek.ReportGenerator.ps1 -InputDir GameResults -Output Reports\report.xml -CfgOutput Reports\cfg.xml ConfigFiles -Format XML
```
