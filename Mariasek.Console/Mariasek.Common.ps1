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
