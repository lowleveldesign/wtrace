function Update-AssemblyInfoVersionFiles ([string]$versionIdentifier)
{
  $local:srcPath = $pwd
  $today = [DateTime]::Today
  $local:buildNumber = "{0:yy}{1}.{2}" -f $today,$today.DayOfYear,($env:GITHUB_RUN_NUMBER % [int16]::MaxValue)

  Write-Host "Executing Update-AssemblyInfoVersionFiles in path $srcPath for build $buildNumber"

  foreach ($file in $(Get-ChildItem $srcPath *.csproj -recurse))
  {
    $local:r = [regex]"<$versionIdentifier>([0-9]+\.[0-9]+)(\.([0-9]+|\*))+"
    $local:assemblyVersion = "0.0.0.0"
    Write-Host "Processing '$($file.FullName)'"
    #version replacements
    (Get-Content -Encoding utf8 $file.FullName) | % {
      $m = $r.Matches($_)
      if ($m -and $m.Success) {
        $assemblyVersion = "$($m.Groups[1].Value).$buildNumber"
        $local:s = $r.Replace($_, "<$versionIdentifier>`$1.$buildNumber")
        Write-Host "Change version to $s"
        $s
      } else {
        $_
      }
    } | Set-Content -Encoding utf8 $file.FullName -Force
  }
}

Update-AssemblyInfoVersionFiles "FileVersion" -Verbose
Update-AssemblyInfoVersionFiles "AssemblyVersion" -Verbose
