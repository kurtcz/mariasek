#requires -version 4.0
param(
	[string]$inputDir = "GameDefinitions",
	[string]$outputDir = "ClassifiedGames"
)

#Include common functions
. .\Mariasek.Common.ps1

$startTime = $(Get-Date)
ExitIfNoDirectoryExists $inputDir

$games = Get-ChildItem $inputDir
EnsureDirectoryExists $outputDir
Write-Host Classifying $games.Count games from $inputDir/, and saving to $outputDir/ ...
foreach($game in $games)
{
	$filename = $game.FullName
	Write-Host Processing $game.FullName ...
	Write-Host .\Mariasek.Console.exe load -FileName="$filename" -Classify -OutputDir="$outputDir" | Out-Null
	& .\Mariasek.Console.exe load -FileName="$filename" -Classify -OutputDir="$outputDir" | Out-Null
}

$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)

if($reportDir)
{
	& .\Mariasek.ReportGenerator.ps1 -InputDir $outputDir -ConfigDir $configDir -Output $reportDir/report$scenario.json -CfgOutput $reportDir/cfg$scenario.json
}