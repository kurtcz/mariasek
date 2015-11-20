function TerminateWithError
{
	param(
		[string]$error
	)
	$Host.UI.WriteErrorLine($error)
	exit -1
}

function ExitIfNoDirectoryExists
{
	param(
		[string]$directory
	)
	if(!(Test-Path -Path $directory))
	{
		$error = "Error: $directory/ does not exist."
		TerminateWithError $error
	}
}

function EnsureDirectoryExists
{
	param(
		[string]$directory
	)
	if(!(Test-Path -Path $directory))
	{
		Write-Host Creating directory $directory/
		New-Item $directory -Type Directory | Out-Null
	}
}

function ExpandProperties
{
	process {
		$properties = New-Object PSObject
		$_.PSObject.Properties |
			ForEach-Object {
				$propertyName = $_.Name
				$propertyValue = $_.Value
                       
				if ($propertyValue -NE $NULL) { # Process values, empty strings and zeros.
					# Create an array of strings with one or more values.
					$values = @()
					foreach ($value in $propertyValue) {
						$values += $value.ToString()
					}
					Add-Member -inputObject $properties NoteProperty -name $propertyName -value "$([string]::Join(';',$values))"
				} else { # Process $NULL values only.
					Add-Member -inputObject $properties NoteProperty -name $propertyName -value $NULL
				}
			}                 
		Write-Output $properties
	}
}

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

function LoadPlayerConfig
{
	param(
		[string]$file
	)
	
	#Write-Host Reading config file $file
	[xml]$xml = Get-Content -Path $file
	$playerConfigData = @(
		PlayerConfigToHashTable $xml.configuration.players.player1;
		PlayerConfigToHashTable $xml.configuration.players.player2;
		PlayerConfigToHashTable $xml.configuration.players.player3;
	)

	return $playerConfigData
}
