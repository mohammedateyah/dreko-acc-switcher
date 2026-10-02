param(
    [Parameter(Mandatory = $true)]
    [string]$Executable,

    [switch]$Unpin
)

$ErrorActionPreference = 'Stop'

try {
    $shell = New-Object -ComObject Shell.Application
    $folder = $shell.Namespace((Split-Path -LiteralPath $Executable -Parent))
    if ($null -eq $folder) {
        throw "Could not open the application folder: $Executable"
    }

    $item = $folder.ParseName((Split-Path -LiteralPath $Executable -Leaf))
    if ($null -eq $item) {
        throw "Could not find the application: $Executable"
    }

    $verbs = @($item.Verbs())
    $pinVerb = $verbs | Where-Object {
        (($_.Name -replace '&', '') -match '(?i)pin to taskbar|شريط المهام') -and
        (($_.Name -replace '&', '') -notmatch '(?i)unpin|إلغاء')
    } | Select-Object -First 1
    $unpinVerb = $verbs | Where-Object {
        (($_.Name -replace '&', '') -match '(?i)unpin.*taskbar|إلغاء.*شريط المهام') -or
        (($_.Name -replace '&', '') -match '(?i)taskbar.*unpin|شريط المهام.*إلغاء')
    } | Select-Object -First 1

    if ($Unpin) {
        if ($null -ne $unpinVerb) {
            $unpinVerb.DoIt()
        }
        exit 0
    }

    if ($null -ne $pinVerb) {
        $pinVerb.DoIt()
        exit 0
    }

    if ($null -ne $unpinVerb) {
        exit 0
    }

    Write-Error 'Windows does not expose a taskbar pin action for this application.'
    exit 1
}
catch {
    Write-Error $_
    exit 1
}
