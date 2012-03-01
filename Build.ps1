param( [string]$buildTask= "default", [int]$buildNumber = 0 )

.\o.exe add-remote -Name localShare -Href file://fisimpbldw3d/wraps/


Import-Module .\tools\psake\psake.psm1
$psake.use_exit_on_error = $true
Invoke-Psake BuildTasks.ps1 $buildTask -framework "4.0" -properties @{ buildNumber=$buildNumber }
Remove-Module psake