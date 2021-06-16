param ([Parameter(Mandatory = $True, ValueFromPipeline = $True, Position = 0)][string]$FilePath)

$algs = "MD5","SHA1","SHA256"

$hashes = $algs | % { Get-FileHash -Algorithm $_ $FilePath }

for ($i = 0; $i -lt $hashes.Length; $i++) {
    $hash = $hashes[$i]
    "$($hash.Algorithm) = $($hash.Hash)"
}
