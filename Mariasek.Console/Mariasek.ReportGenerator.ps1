param(
	[string]$inputDir = "GameResults",
	[string]$configDir = "ConfigFiles",
	[string]$output = ".\_report.json"
)

#Include the common functions
. .\Mariasek.Common.ps1

$gameData = {@()}.Invoke()

function PlayerConfigToHashTable
{
	param(
		[System.Xml.XmlElement]$xml
	)

	$config = @{
		'Name' = $xml.Name;
		'Position' = $xml.get_Name();
		'Type' = $xml.type;
		'Assembly' = $xml.assembly;
		'AiCheating' = $xml.selectSingleNode("./parameter[@name='AiCheating']/@value").get_innerXml();
		'RoundsToCompute' = $xml.selectSingleNode("./parameter[@name='RoundsToCompute']/@value").get_innerXml();
		'CardSelectionStrategy' = $xml.selectSingleNode("./parameter[@name='CardSelectionStrategy']/@value").get_innerXml();
		'SimulationsPerRound' = $xml.selectSingleNode("./parameter[@name='SimulationsPerRound']/@value").get_innerXml();
		'RuleThreshold' = $xml.selectSingleNode("./parameter[@name='RuleThreshold']/@value").get_innerXml();
		'GameThreshold' = $xml.selectSingleNode("./parameter[@name='GameThreshold']/@value").get_innerXml();
	}

	return New-Object PSObject -Property $config
}

function LoadPlayerConfigForGame
{
	param(
		[System.IO.FileInfo]$file
	)
	
	$baseName = $file.BaseName.Split("-")
	
	if ($baseName.Count -lt 2)
	{
		TerminateWithError ("Cannot parse configuration name out of game file {0}" -f $file.FullName)
	}
	$configName = "{0}/{1}.config" -f $configDir, $baseName[1]
	Write-Host Reading config file $configName
	[xml]$xml = Get-Content -Path $configName
	$playerConfigData = @(
		PlayerConfigToHashTable $xml.configuration.players.player1;
		PlayerConfigToHashTable $xml.configuration.players.player2;
		PlayerConfigToHashTable $xml.configuration.players.player3;
	)

	return $playerConfigData
}

function PopulateGameData
{
	param(
		$games
	)

	foreach($game in $games)
	{
		$playerConfigData = LoadPlayerConfigForGame $game.FullName
		[xml]$xml = Get-Content -Path $game.FullName
		$player1 = $playerConfigData | Where-Object { $_.Position -eq 'player1' }
		$player2 = $playerConfigData | Where-Object { $_.Position -eq 'player2' }
		$player3 = $playerConfigData | Where-Object { $_.Position -eq 'player3' }

		$player1Data = New-Object PSObject -Property @{
			'GameId' = $game.BaseName.Split("-")[0];
			'Typ' = $xml.Hra.Typ;
			'Player' = $player1.Name;
			'Position' = $player1.Position;
			'Score' = [int]$xml.Hra.Zuctovani.Hrac1.Body;
			'Money' = [int]$xml.Hra.Zuctovani.Hrac1.Zisk;
		}
		$gameData.Add($player1Data)
		$player2Data = New-Object PSObject -Property @{
			'GameId' = $game.BaseName.Split("-")[0];
			'Typ' = $xml.Hra.Typ;
			'Player' = $player2.Name;
			'Position' = $player2.Position;
			'Score' = [int]$xml.Hra.Zuctovani.Hrac2.Body;
			'Money' = [int]$xml.Hra.Zuctovani.Hrac2.Zisk;
		}
		$gameData.Add($player2Data)
		$player3Data = New-Object PSObject -Property @{
			'GameId' = $game.BaseName.Split("-")[0];
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
$configs = Get-ChildItem $configDir/* -Include "*.config"
$count = $configs.Count
Write-Host $count configs found in $configDir/

$games = Get-ChildItem $inputDir/* -Include "*.hra"
$count = $games.Count
Write-Host $count games found in $inputDir/
PopulateGameData $games

Write-Host Writing JSON Data to $output
$json = ConvertTo-Json $gameData
Set-Content -Path $output $json


$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)
