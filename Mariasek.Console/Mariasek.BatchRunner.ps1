param(
	[string]$inputDir = "GameDefinitions",
	[string]$outputDir = "GameResults",
	[string]$configDir = "ConfigFiles",
	[string]$reportDir = $null,
	[string]$scenario = ""
)

#Include common functions
. .\Mariasek.Common.ps1

$startTime = $(Get-Date)
ExitIfNoDirectoryExists $inputDir
ExitIfNoDirectoryExists $configDir

$games = Get-ChildItem $inputDir
$configs = Get-ChildItem $configDir/* -Include "*.config"
EnsureDirectoryExists $outputDir
Write-Host Playing $games.Count games from $inputDir/, using $configs.Count config files and saving to $outputDir/ ...
foreach($game in $games)
{
	foreach($config in $configs)
	{
		$gameResult = "{0}-{1}.hra" -f $game.BaseName, $config.BaseName
		$filename = $game.FullName
		& .\Mariasek.Console.exe load -FileName="$filename" -config="$config.FullName" -Result="$outputDir/$gameResult" | Out-Null
		Write-Host $gameResult saved to $outputDir/
	}
}

$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)

if($reportDir)
{
	& .\Mariasek.ReportGenerator.ps1 -InputDir $outputDir -ConfigDir $configDir -Output $reportDir/report$scenario.json -CfgOutput $reportDir/cfg$scenario.json
}