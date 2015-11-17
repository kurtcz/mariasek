param(
	[string]$inputDir = "GameDefinitions",
	[string]$outputDir = "GameResults",
	[string]$configDir = "ConfigFiles"
)

function ExitIfNoDirectoryExists
{
	param(
		[string]$directory
	)
	if(!(Test-Path -Path $directory))
	{
		$error = "Error: $directory/ does not exist."
		$Host.UI.WriteErrorLine($error)
		exit -1
	}
}

$startTime = $(Get-Date)
ExitIfNoDirectoryExists $inputDir
ExitIfNoDirectoryExists $configDir

if(!(Test-Path -Path $outputDir))
{
	Write-Host Creating directory $outputDir/
	New-Item $outputDir -Type Directory | Out-Null
}
$games = Get-ChildItem $inputDir
$configs = Get-ChildItem $configDir/* -Include "*.config"
Write-Host Playing $games.Count games from $inputDir/, using $configs.Count config files and saving to $outputDir/ ...
foreach($game in $games)
{
	foreach($config in $configs)
	{
		$gameResult = "{0}-{1}.hra" -f $game.BaseName, $config.BaseName
		$filename = $game.FullName
		& ./Mariasek.Console.exe load -FileName="$filename" -Result="$outputDir/$gameResult" | Out-Null
		Write-Host $gameResult saved to $outputDir/
	}
}
$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)