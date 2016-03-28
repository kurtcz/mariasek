#requires -version 4.0
param(
	[int]$gamesToGenerate = 10,
	[string]$outputDir = "GameDefinitions",
	[string]$gameType = $null
)

$startTime = $(Get-Date)

if(!(Test-Path -Path $outputDir))
{
	Write-Host Creating directory $outputDir/
	New-Item $outputDir -Type Directory | Out-Null
}
Write-Host Generating $gamesToGenerate games to $outputDir/ ...
for ($i = 1; $i -le $gamesToGenerate; $i++)
{	
	if($gameType -eq $null)
	{
		& .\Mariasek.Console.exe -SkipGame | Out-Null
		$filename = ("{0:0000}.hra" -f $i)
		Copy-Item _temp.hra -Destination $outputDir\$filename -Force
	}
	else
	{
		& .\Mariasek.Console.exe -GameType="$gameType" | Out-Null
		$filename = ("{0:0000}-{1}.hra" -f $i, $gameType)
		Copy-Item _temp.hra -Destination $outputDir\$filename -Force
		Write-Host $filename saved to $outputDir/
	}
}

$elapsedTime = $(Get-Date) - $startTime
Write-Host Finished in ("{0:hh\:mm\:ss}" -f $elapsedTime)