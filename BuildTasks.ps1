@'
    Modified slightly to allowed to build FSharp sln.
        (Regex to get version number is different)
'@

properties { 
	$projectName = "Simpliciti.OnDemand.Lite"
	$buildNumber = 0
    
	$rootDir  = Resolve-Path .
	$srcDir = "$rootDir\src"
    
    $buildOutputDir = "$rootDir\buildOutput"
	$solutionFilePath = "$srcDir\$projectName.sln"
  
    
    $toolDir = "$rootDir\tools"
    $nunitDir = "$toolDir\nunit"
    
    $env:Path += ";$nunitDir;"
}

task default -depends UpdateVersionNumbers, UpdateDependencies, BuildWrap
task UpdateDependencies -depends UpdateWraps

task buildwrapLocal -depends UpdateVersionNumbers, UpdateDependencies, BuildWrap

task UpdateWraps {
    exec { .\o.exe update-wrap openwrap -project }
    exec { .\o.exe clean-wrap  }
    exec { .\o.exe update-wrap }
    exec { .\o.exe clean-wrap  }
}

task GetUnmangedDependencies {
    Remove-Item ".\wraps\_cache\Simpliciti.RiskGeneration\*" -recurse -force -ErrorAction 0
    Remove-Item ".\wraps\Simpliciti.RiskGeneration\" -recurse -force -ErrorAction 0
    Remove-Item ".\wraps\_cache\ark\*" -recurse -force -ErrorAction 0
    Remove-Item ".\wraps\ark\*" -recurse -force -ErrorAction 0
    
    Copy-Item ".\pullIn.wrapdesc.temp"  -Destination "pullIn.wrapdesc"
    exec { .\o.exe update-wrap -Project}
    Remove-Item ".\pullIn.wrapdesc"
}

task Clean {
	Remove-Item $rootDir\TestResult.xml -Force -ErrorAction SilentlyContinue
    Remove-Item $buildOutputDir -Force -Recurse -ErrorAction SilentlyContinue
	exec { msbuild $solutionFilePath /t:Clean }
}

task UpdateVersionNumbers {
	$assemblyInfoFilePath = "$srcDir\SharedAssemblyInfo.fs"
    $assemblyInfoCS = "$srcDir\SharedAssemblyInfo.cs"
    
	$version = Get-SourceVersion $assemblyInfoFilePath
	$oldVersion = New-Object Version $version
	$newVersion = New-Object Version ($oldVersion.Major, $oldVersion.Minor, $oldVersion.Build, $buildNumber)
	
    Update-SourceVersion $newVersion $assemblyInfoFilePath
	Update-SourceVersion $newVersion $assemblyInfoCS
	Set-Content Version $newVersion
}

task Compile -depends Clean {
	exec { msbuild $solutionFilePath /p:Configuration=Release }
}

task RunTests {
    $testAssemblies = (gci -Path "src\*Tests*" -Include Simpliciti.OnDemand.Lite*.Tests.dll -recurse  | foreach {$_.FullName} | Select-String "bin")
    Write-Host $testAssemblies
    Write-Host "-----------"
     foreach($test_asm_name in $testassemblies) {
        $file_name =  [system.io.path]::getfilename($test_asm_name.tostring())
        $output_xml = "{0}.xml" -f $file_name
        write-host "running tests: $srcdir\$test_asm_name"
        exec { nunit-console-x86.exe $test_asm_name /exclude:ignore /noshadow /nothread /xml:$output_xml} 
     }
}

task TestFormatResult {
    exec { &"$tooldir\nxslt3\nxslt3.exe" $rootDir\TestResult.xml $toolDir\nxslt3\NUnitSummary.xslt -o Results.html }
}

task BuildWrap {
    exec { .\o.exe build-wrap }
	New-Item $buildOutputDir -type directory
	Move-Item *.wrap $buildOutputDir
}

task PublishWrap -depends BuildWrap {
    Write-Host $newVersion
    pushd $buildOutputDir
     exec { ..\o.exe publish-wrap -Remote localShare -Name $projectName }
    popd
}

function Update-SourceVersion
{
	param (
		[string]$version,
		[string]$assemblyInfoFilePath
		)
	
	$newVersion = 'AssemblyVersion("' + $version + '")';
	$newFileVersion = 'AssemblyFileVersion("' + $version + '")';
	$tmpFile = $assemblyInfoFilePath + ".tmp"

	Get-Content $assemblyInfoFilePath | 
		%{$_ -replace 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newVersion } |
		%{$_ -replace 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)', $newFileVersion }  > $tmpFile

	Move-Item $tmpFile $assemblyInfoFilePath -force
}

function Get-SourceVersion
{
	param
	(
		[string]$assemblyInfoFilePath
	)
	Write-Host "path $assemblyInfoFilePath"
	$pattern = '(?<=\[\<?assembly\: AssemblyVersion\(\")(?<versionString>\d+\.\d+\.\d+\.\d+)(?=\"\))'
	$assmblyInfoContent = Get-Content $assemblyInfoFilePath
	return $assmblyInfoContent | Select-String -Pattern $pattern | Select -expand Matches |% {$_.Groups['versionString'].Value}
}
