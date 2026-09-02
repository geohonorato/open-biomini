$cert = Get-ChildItem Cert:\CurrentUser\My\E44BC835F9B48D2CE9D911DDB8DFE739C030F382
$rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('Root', 'LocalMachine')
$rootStore.Open('ReadWrite')
$rootStore.Add($cert)
$rootStore.Close()
$pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store('TrustedPublisher', 'LocalMachine')
$pubStore.Open('ReadWrite')
$pubStore.Add($cert)
$pubStore.Close()
Write-Host 'Certificado instalado em Root e TrustedPublisher com sucesso'
