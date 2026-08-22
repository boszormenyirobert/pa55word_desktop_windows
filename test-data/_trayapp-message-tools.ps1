Set-StrictMode -Version Latest

$script:PipeName = "ZeroIntrusion.TrayApp.NativeMessaging"
$script:ConnectTimeoutSeconds = 10
$script:ConnectRetryDelayMilliseconds = 250
$script:StateFilePath = Join-Path $PSScriptRoot "trayapp-test-state.json"

function ConvertTo-CompactJson {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject
    )

    return $InputObject | ConvertTo-Json -Depth 12 -Compress
}

function Connect-TrayAppPipe {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($script:ConnectTimeoutSeconds)

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $client = [System.IO.Pipes.NamedPipeClientStream]::new(
            ".",
            $script:PipeName,
            [System.IO.Pipes.PipeDirection]::InOut,
            [System.IO.Pipes.PipeOptions]::Asynchronous)

        try {
            $client.Connect($script:ConnectRetryDelayMilliseconds)
            if ($client.IsConnected) {
                return $client
            }
        }
        catch {
        }

        $client.Dispose()
    }

    throw "TrayApp is not reachable on pipe '$script:PipeName'. Start the application first."
}

function Write-FramedMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$Stream,

        [Parameter(Mandatory = $true)]
        [string]$Json
    )

    $payload = [System.Text.Encoding]::UTF8.GetBytes($Json)
    $lengthBytes = [System.BitConverter]::GetBytes($payload.Length)

    $Stream.Write($lengthBytes, 0, $lengthBytes.Length)
    $Stream.Write($payload, 0, $payload.Length)
    $Stream.Flush()
}

function Read-ExactBytes {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$Stream,

        [Parameter(Mandatory = $true)]
        [int]$Length
    )

    $buffer = New-Object byte[] $Length
    $offset = 0

    while ($offset -lt $Length) {
        $read = $Stream.Read($buffer, $offset, $Length - $offset)
        if ($read -le 0) {
            throw "Unexpected end of stream while reading TrayApp response."
        }

        $offset += $read
    }

    return $buffer
}

function Read-FramedMessage {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$Stream
    )

    $lengthBytes = Read-ExactBytes -Stream $Stream -Length 4
    $payloadLength = [System.BitConverter]::ToInt32($lengthBytes, 0)

    if ($payloadLength -le 0) {
        throw "TrayApp returned an invalid payload length: $payloadLength"
    }

    $payload = Read-ExactBytes -Stream $Stream -Length $payloadLength
    return [System.Text.Encoding]::UTF8.GetString($payload)
}

function Invoke-TrayAppMessage {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Payload
    )

    $requestJson = ConvertTo-CompactJson -InputObject $Payload
    $client = Connect-TrayAppPipe

    try {
        Write-FramedMessage -Stream $client -Json $requestJson
        $responseJson = Read-FramedMessage -Stream $client
        $response = $responseJson | ConvertFrom-Json

        if ($response -is [pscustomobject]) {
            $properties = $response.PSObject.Properties.Name
            if (($properties -contains "ok") -and (-not $response.ok)) {
                $errorCode = $response.error.code
                $errorMessage = $response.error.message
                throw "TrayApp error [$errorCode]: $errorMessage"
            }
        }

        return $response
    }
    finally {
        $client.Dispose()
    }
}

function Load-TestState {
    if (-not (Test-Path -LiteralPath $script:StateFilePath)) {
        return [ordered]@{
            createdAt = $null
            groups = @()
            entries = @()
        }
    }

    return Get-Content -LiteralPath $script:StateFilePath -Raw | ConvertFrom-Json
}

function Save-TestState {
    param(
        [Parameter(Mandatory = $true)]
        [object]$State
    )

    $json = $State | ConvertTo-Json -Depth 12
    Set-Content -LiteralPath $script:StateFilePath -Value $json -Encoding UTF8
}

function New-StrongPassword {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Prefix,

        [Parameter(Mandatory = $true)]
        [int]$Index
    )

    $guidPart = [Guid]::NewGuid().ToString("N").Substring(0, 8)
    return "$Prefix!$Index-$guidPart"
}

function Require-Value {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    if ($null -eq $Value) {
        throw "$Name must be set before running the script."
    }

    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) {
        throw "$Name must be set before running the script."
    }

    if ($Value -is [int] -and $Value -le 0) {
        throw "$Name must be greater than zero before running the script."
    }
}