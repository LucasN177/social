# Fasst alle nummerierten Skripte aus supabase/sql zu supabase/setup.sql zusammen.
# Aufruf (aus dem Repo-Root):  powershell -ExecutionPolicy Bypass -File supabase/build-setup.ps1
# setup.sql nie von Hand bearbeiten - Aenderungen immer als neues nummeriertes Skript anlegen und neu erzeugen.
# Hinweis: Diese Datei bewusst nur ASCII, da Windows PowerShell 5.1 Skripte ohne BOM als ANSI liest.

$ErrorActionPreference = 'Stop'
$sqlDir = Join-Path $PSScriptRoot 'sql'
$target = Join-Path $PSScriptRoot 'setup.sql'
$utf8 = New-Object System.Text.UTF8Encoding($false)

$scripts = Get-ChildItem -Path $sqlDir -Filter '*.sql' | Where-Object { $_.Name -match '^\d{3}_' } | Sort-Object Name

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-- =====================================================================')
$lines.Add('-- setup.sql - komplettes Datenbank-Setup fuer Social (Supabase)')
$lines.Add('-- AUTOMATISCH ERZEUGT von supabase/build-setup.ps1 - nicht von Hand bearbeiten.')
$lines.Add('-- Quelle: supabase/sql/' + $scripts[0].Name + ' bis ' + $scripts[-1].Name)
$lines.Add('--')
$lines.Add('-- Ausfuehren: Supabase Dashboard > SQL Editor > Inhalt einfuegen > Run.')
$lines.Add('-- Idempotent: kann beliebig oft erneut ausgefuehrt werden.')
$lines.Add('-- Laeuft in einer Transaktion - bei einem Fehler wird nichts uebernommen.')
$lines.Add('-- =====================================================================')
$lines.Add('')
$lines.Add('begin;')

foreach ($script in $scripts) {
    $content = [System.IO.File]::ReadAllText($script.FullName, $utf8).Replace("`r`n", "`n").TrimEnd()
    $lines.Add('')
    # Jedes Skript beginnt selbst mit "-- <dateiname>"
    $lines.Add('-- ---------------------------------------------------------------------')
    $lines.Add($content)
}

$lines.Add('')
$lines.Add('commit;')

[System.IO.File]::WriteAllText($target, ($lines -join "`n") + "`n", $utf8)
Write-Host ('setup.sql erzeugt aus ' + $scripts.Count + ' Skripten.')
