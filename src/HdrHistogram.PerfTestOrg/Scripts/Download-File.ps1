param(
	[string]$address,
	[string]$destination
	) 
$url = $address -as [System.URI] 
if (!$url) { 
  throw "File to download must be a Uri - '$address'";
} 
if (-not (test-path $destination)) { 
  New-Item -ItemType directory -Path $destination;
}


function expand-zip([string]$file, [string]$destination) {
	$shell = new-object -com shell.application;

	"Extracting '$file' to '$destination'"

	$zip = $shell.NameSpace($file);
	$target = $shell.Namespace($destination);

	foreach($item in $zip.items())
	{
		$target.copyhere($item);
	}
}

 

$curDir = Resolve-Path .
$fileName = [System.IO.Path]::GetFileName($url.LocalPath)
$tempFilePath = Join-Path $curDir -ChildPath $fileName

$webclient = New-Object System.Net.WebClient
$webclient.DownloadFile($url,$tempFilePath)

expand-zip -file $tempFilePath -destination $destination

Remove-Item -path $tempFilePath
