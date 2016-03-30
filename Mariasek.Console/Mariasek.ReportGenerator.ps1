#requires -version 4.0
param(
	[string]$inputDir = "GameResults",
	[string]$configDir = "ConfigFiles",
	[string]$output = ".\report.json",
	[string]$cfgoutput = ".\cfg.json",
	[string]$format = "json"
)

#Include common functions
. .\Mariasek.Common.ps1

$gameData = {@()}.Invoke()

function PopulateGameData
{
	param(
		$games
	)

	foreach($game in $games)
	{
		#Filename has the following pattern: <GameId>-<ConfigId>.hra
		$gameIds = $game.BaseName.Split("-")
		Write-Host ("Populating game data for {0}{1}" -f $game.BaseName, $game.Extension)
	
		if ($gameIds.Count -lt 2)
		{
			TerminateWithError ("Cannot parse configuration name out of game file {0}" -f $file.FullName)
		}
		$configPath = "{0}/{1}.config" -f $configDir, $gameIds[$gameIds.Count-1]

		$playerConfigData = LoadPlayerConfig $configPath
		$player1 = $playerConfigData | Where-Object { $_.Position -eq 'player1' }
		$player2 = $playerConfigData | Where-Object { $_.Position -eq 'player2' }
		$player3 = $playerConfigData | Where-Object { $_.Position -eq 'player3' }

		[xml]$xml = Get-Content -Path $game.FullName
		$player1Data = New-Object PSObject -Property @{
			'GameId' = [string]::Join("-", $gameIds, 0, $gameIds.Count-1);
			'ConfigId' = $gameIds[$gameIds.Count-1];
			'Typ' = $xml.Hra.Typ;
			'Player' = $player1.Name;
			'Position' = $player1.Position;
			'Score' = [int]$xml.Hra.Zuctovani.Hrac1.Body;
			'Money' = [int]$xml.Hra.Zuctovani.Hrac1.Zisk;
		}
		$gameData.Add($player1Data)
		$player2Data = New-Object PSObject -Property @{
			'GameId' = [string]::Join("-", $gameIds, 0, $gameIds.Count-1);
			'ConfigId' = $gameIds[$gameIds.Count-1];
			'Typ' = $xml.Hra.Typ;
			'Player' = $player2.Name;
			'Position' = $player2.Position;
			'Score' = [int]$xml.Hra.Zuctovani.Hrac2.Body;
			'Money' = [int]$xml.Hra.Zuctovani.Hrac2.Zisk;
		}
		$gameData.Add($player2Data)
		$player3Data = New-Object PSObject -Property @{
			'GameId' = [string]::Join("-", $gameIds, 0, $gameIds.Count-1);
			'ConfigId' = $gameIds[$gameIds.Count-1];
			'Typ' = $xml.Hra.Typ;
			'Player' = $player3.Name;
			'Position' = $player3.Position;
			'Score' = [int]$xml.Hra.Zuctovani.Hrac3.Body;
			'Money' = [int]$xml.Hra.Zuctovani.Hrac3.Zisk;
		}
		$gameData.Add($player3Data)
	}
}

$startTime = $(Get-Date)
ExitIfNoDirectoryExists $inputDir
ExitIfNoDirectoryExists $configDir

Write-Host Generating report ...
#$configs = Get-ChildItem $configDir/* -Include "*.config"
#$count = $configs.Count
#Write-Host $count configs found in $configDir/
$configPath = "{0}/abc.config" -f $configDir
$playerConfigData = LoadPlayerConfig $configPath
 
$games = Get-ChildItem $inputDir/* -Include "*.hra"
$count = $games.Count
Write-Host $count games found in $inputDir/
PopulateGameData $games

#Serialize game data to file
switch($format)
{
	"json"
	{
		Write-Host Writing JSON Data to $output and $cfgoutput
		$str = $gameData | ExpandProperties | ConvertTo-Json
		$cfg = $playerConfigData | ConvertTo-Json
	}
	"xml"
	{
		Write-Host Writing XML Data to $output and $cfgoutput
		$str = $gameData | ExpandProperties | ConvertTo-Xml -As String -NoTypeInformation
		$cfg = $playerConfigData | ConvertTo-Xml -As String -NoTypeInformation
	}
	"csv"
	{
		Write-Host Writing CSV Data to $output and $cfgoutput
		$str = $gameData | ExpandProperties | ConvertTo-Csv -NoTypeInformation
		$cfg = $playerConfigData | ConvertTo-Csv -NoTypeInformation
	}
}
$outputDir = Split-Path $output
$cfgOutputDir = Split-Path $cfgoutput
EnsureDirectoryExists $outputDir
EnsureDirectoryExists $cfgOutputDir
Set-Content -Path $output $str
Set-Content -Path $cfgoutput $cfg

$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)
