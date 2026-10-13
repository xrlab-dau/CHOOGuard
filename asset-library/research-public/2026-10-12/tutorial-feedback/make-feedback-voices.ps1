$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType=WindowsRuntime] | Out-Null
[Windows.Media.SpeechSynthesis.SpeechSynthesisStream, Windows.Media.SpeechSynthesis, ContentType=WindowsRuntime] | Out-Null
$tutorialAwaitMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
} | Select-Object -First 1
$tutorialSynth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$tutorialSynth.Voice = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices | Where-Object Language -eq 'ko-KR' | Select-Object -First 1
if ($null -eq $tutorialSynth.Voice) { throw 'An installed Korean voice is required.' }
$tutorialSynth.Options.SpeakingRate = 1.2
$tutorialLines = @(
    @('guide-00', '무전기의 전원과 배터리를 확인하세요. 근무 전에 연락할 준비를 합니다.'),
    @('guide-01', '객실 스피커의 방송 출력을 확인하세요. 방송이 끝나면 인터폰을 점검합니다.'),
    @('guide-02', '방송이 끝났습니다. 인터폰 호출 버튼을 누르고 점검 통화와 상대의 응답을 확인하세요.'),
    @('guide-03', '소화기 수납함의 위치와 접근 공간을 살펴보세요. 분사하지 않습니다.'),
    @('guide-05', '객실 앞쪽으로 이동하세요. 좌석과 창가, 선반을 표식에 따라 하나씩 관찰합니다.'),
    @('guide-06', '객실 중앙으로 이동하세요. 반대쪽 좌석과 주변 설비도 같은 방법으로 살펴봅니다.'),
    @('guide-07', '영상처럼 객실이 더운 상황입니다. 냉난방 제어반에서 온도 설정을 낮춰 보세요.'),
    @('guide-08', '문이 덜 닫혀 있고 짐이 막고 있습니다. 문과 짐을 확인해 보세요.'),
    @('guide-09', '큐 키를 누른 채 무전 문장을 선택하세요. 출발 보류를 고르고 큐 키를 떼면 전송됩니다. 문을 억지로 닫으면 안 됩니다.'),
    @('guide-10', '문 옆 취급 장치로 문을 개방하세요. 열리는 모습을 확인합니다.'),
    @('guide-11', '승객에게 짐 정리를 요청하세요. 짐이 이동하는 모습을 확인합니다.'),
    @('guide-12', '짐이 정리되었습니다. 문을 닫고 닫힘 상태를 확인하세요.'),
    @('guide-13', '큐 키를 누른 채 짐 정리와 문 닫힘 확인 보고를 선택하세요. 큐 키를 떼면 전송되고 상대의 응답을 들을 수 있습니다.'),
    @('reply-intercom', '승무원님, 비상통화 점검 중입니다. 잘 들립니다. 통화 상태를 확인했습니다.'),
    @('reply-pa', '방송 장치 점검 중입니다. 객실 출력을 확인해 주세요.'),
    @('reply-hold', '출입문 이상. 출발 보류 바랍니다. 수신했습니다. 출발 보류합니다.'),
    @('reply-luggage', '문 주변의 짐을 정리해 주세요. 네, 옆으로 옮기겠습니다.'),
    @('reply-complete', '짐 정리와 문 닫힘 확인을 마쳤습니다. 수신했습니다.')
)

$tutorialProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$tutorialFolder = Join-Path $tutorialProject 'Assets\ChooGuard\Art\Tutorial\Audio'
New-Item -ItemType Directory -Path $tutorialFolder -Force | Out-Null
$tutorialReceipts = @()
try {
    foreach ($tutorialLine in $tutorialLines) {
        $tutorialTask = $tutorialAwaitMethod.MakeGenericMethod([Windows.Media.SpeechSynthesis.SpeechSynthesisStream]).Invoke($null, @($tutorialSynth.SynthesizeTextToStreamAsync($tutorialLine[1])))
        $tutorialTask.Wait()
        $tutorialAudio = $tutorialTask.Result
        $tutorialStream = [System.IO.WindowsRuntimeStreamExtensions]::AsStreamForRead($tutorialAudio)
        $tutorialPath = Join-Path $tutorialFolder ($tutorialLine[0] + '.wav')
        $tutorialFile = [IO.File]::Create($tutorialPath)
        try { $tutorialStream.CopyTo($tutorialFile) } finally { $tutorialFile.Dispose(); $tutorialStream.Dispose(); $tutorialAudio.Dispose() }
        if ((Get-Item -LiteralPath $tutorialPath).Length -lt 1000) { throw ('Empty speech: ' + $tutorialLine[0]) }
        $tutorialReceipts += [ordered]@{ file=$tutorialLine[0]+'.wav'; text=$tutorialLine[1]; bytes=(Get-Item -LiteralPath $tutorialPath).Length; sha256=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($tutorialPath))).Replace('-','').ToLowerInvariant(); contentType='audio/wav'; engine='Windows.Media.SpeechSynthesis'; voice=$tutorialSynth.Voice.DisplayName; url='https://learn.microsoft.com/en-us/uwp/api/windows.media.speechsynthesis.speechsynthesizer'; license='Generated locally with the installed Windows voice; engine/voice binaries are not redistributed.'; generatedAt=[DateTime]::UtcNow.ToString('o') }
    }
} finally { $tutorialSynth.Dispose() }
$tutorialResearch = $PSScriptRoot
New-Item -ItemType Directory -Path $tutorialResearch -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $tutorialResearch 'receipts.json'), ($tutorialReceipts | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
'Generated ' + $tutorialReceipts.Count + ' offline Korean clips.'

