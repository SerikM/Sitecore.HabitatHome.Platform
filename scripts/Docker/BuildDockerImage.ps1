param(
      [string]$baseImageName, 
      [string]$tag, 
      [string]$buildContext
    )

$ErrorActionPreference = 'Stop'

$ScriptPath = Split-Path $MyInvocation.MyCommand.Path

Import-Module $ScriptPath\BuildImage.psm1

New-Image -BaseImageName $baseImageName -Tag $tag -BuildContext $buildContext