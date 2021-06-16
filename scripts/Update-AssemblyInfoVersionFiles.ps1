function Update-AssemblyInfoVersionFiles ([string]$versionIdentifier)
{
  $local:srcPath = $pwd
  $today = [DateTime]::Today
  $local:buildNumber = "{0:yy}{1}.{2}" -f $today,$today.DayOfYear,($env:GITHUB_RUN_NUMBER % [int16]::MaxValue)
  $local:tagVersion = ([System.IO.Path]::GetFileName("$env:GITHUB_REF") -split "-")[0]
  $local:version = "$tagVersion.$buildNumber"

  Write-Host "Executing Update-AssemblyInfoVersionFiles in path $srcPath for version $version"

  foreach ($file in $(Get-ChildItem -Path $srcPath -Include "*.csproj","*.nuspec" -recurse))
  {
    $local:r = [regex]"<$versionIdentifier>([0-9]+\.[0-9]+)(\.([0-9]+|\*))+"
    Write-Host "Processing '$($file.FullName)'"
    #version replacements
    (Get-Content -Encoding utf8 $file.FullName) | % {
      $m = $r.Matches($_)
      if ($m -and $m.Success) {
        $local:s = $r.Replace($_, "<$versionIdentifier>$version")
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
Update-AssemblyInfoVersionFiles "version" -Verbose
