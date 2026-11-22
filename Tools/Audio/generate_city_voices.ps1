# Original crowd dialogue, synthesized locally with the Windows desktop speech voices.
# Run from the repository root with Windows PowerShell 5.1. No downloaded recordings.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Speech
$voiceOutput = Join-Path (Get-Location) 'Assets/Resources/WorldShiftCityVoices'
New-Item -ItemType Directory -Force -Path $voiceOutput | Out-Null
$lines = @(
    "Did you see that?", "Wait, over here!", "Two coffees, please.", "I'll call you back.",
    "No, the other way.", "I was just saying.", "That's my taxi!", "Then wave faster!",
    "Meet me on the corner.", "Yeah, about ten minutes.", "Watch the jacket!", "You started it!",
    "You hear that siren?", "There goes the peace.", "Bus late again?", "Every single day.",
    "So what happened next?", "Come on, we're late!", "Sorry, excuse me.", "Let's grab something to eat."
)
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $voiceName = @('Microsoft David Desktop', 'Microsoft Zira Desktop')[$i % 2]
        try { $speaker.SelectVoice($voiceName) }
        catch { $speaker.SelectVoice('Microsoft David Desktop') }
        $speaker.Rate = @(-1, 1, 0, 2)[$i % 4]
        $speaker.SetOutputToWaveFile((Join-Path $voiceOutput ('CityVoice_{0:D2}.wav' -f $i)), $format)
        $pitch = @('-8%', '+16%')[$i % 2]
        $escaped = [System.Security.SecurityElement]::Escape($lines[$i])
        $speaker.SpeakSsml('<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US"><prosody pitch="' + $pitch + '">' + $escaped + '</prosody></speak>')
        $speaker.SetOutputToNull()
    }
} finally { $speaker.Dispose() }
node Tools/Audio/mix_city_crowd.cjs
if ($LASTEXITCODE -ne 0) { throw 'Crowd audio mix failed.' }
