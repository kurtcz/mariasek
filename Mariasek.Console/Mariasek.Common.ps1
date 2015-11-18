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
