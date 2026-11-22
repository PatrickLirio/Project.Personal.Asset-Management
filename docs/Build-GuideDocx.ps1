$ErrorActionPreference = 'Stop'

$markdownPath = Join-Path $PSScriptRoot 'Asset-Management-Refactor-Guide.md'
$outputPath = Join-Path $PSScriptRoot 'Asset-Management-Refactor-Guide.docx'
$markdownLines = [System.IO.File]::ReadAllLines($markdownPath, [System.Text.Encoding]::UTF8)

function Escape-Xml([string] $value) {
    if ($null -eq $value) { return '' }
    return [System.Security.SecurityElement]::Escape($value)
}

function New-Run([string] $value, [string] $properties = '') {
    $escaped = Escape-Xml $value
    return "<w:r>$properties<w:t xml:space=`"preserve`">$escaped</w:t></w:r>"
}

function Inline-Runs([string] $value) {
    $result = [System.Text.StringBuilder]::new()
    $pattern = '\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+\]\([^)]+\)'
    $position = 0
    foreach ($match in [regex]::Matches($value, $pattern)) {
        if ($match.Index -gt $position) {
            [void]$result.Append((New-Run $value.Substring($position, $match.Index - $position)))
        }

        $token = $match.Value
        if ($token.StartsWith('**')) {
            $props = '<w:rPr><w:b/></w:rPr>'
            [void]$result.Append((New-Run $token.Substring(2, $token.Length - 4) $props))
        }
        elseif ($token.StartsWith('`')) {
            $props = '<w:rPr><w:rStyle w:val="CodeChar"/></w:rPr>'
            [void]$result.Append((New-Run $token.Substring(1, $token.Length - 2) $props))
        }
        else {
            $link = [regex]::Match($token, '^\[([^\]]+)\]\(([^)]+)\)$')
            [void]$result.Append((New-Run ($link.Groups[1].Value + ' (' + $link.Groups[2].Value + ')')))
        }
        $position = $match.Index + $match.Length
    }
    if ($position -lt $value.Length) {
        [void]$result.Append((New-Run $value.Substring($position)))
    }
    if ($result.Length -eq 0) { [void]$result.Append((New-Run ' ')) }
    return $result.ToString()
}

function New-Paragraph([string] $text, [string] $style = 'Normal', [string] $extraProperties = '') {
    $runs = Inline-Runs $text
    return "<w:p><w:pPr><w:pStyle w:val=`"$style`"/>$extraProperties</w:pPr>$runs</w:p>"
}

function New-CodeParagraph([string] $text) {
    $escaped = Escape-Xml $text
    if ($escaped.Length -eq 0) { $escaped = ' ' }
    return "<w:p><w:pPr><w:pStyle w:val=`"CodeBlock`"/></w:pPr><w:r><w:t xml:space=`"preserve`">$escaped</w:t></w:r></w:p>"
}

function New-Table([string[]] $rows) {
    $parsedRows = @()
    foreach ($line in $rows) {
        $trimmed = $line.Trim().Trim('|')
        if ($trimmed -match '^\s*:?-{3,}') { continue }
        $parsedRows += ,@($trimmed.Split('|') | ForEach-Object { $_.Trim() })
    }
    if ($parsedRows.Count -eq 0) { return '' }

    $columnCount = ($parsedRows | ForEach-Object { $_.Count } | Measure-Object -Maximum).Maximum
    $gridColumns = ((1..$columnCount | ForEach-Object { '<w:gridCol w:w="3000"/>' }) -join '')
    $grid = "<w:tblGrid>$gridColumns</w:tblGrid>"
    $tableProperties = '<w:tblPr><w:tblW w:w="0" w:type="auto"/><w:tblBorders><w:top w:val="single" w:sz="4" w:color="B7C9D6"/><w:left w:val="single" w:sz="4" w:color="B7C9D6"/><w:bottom w:val="single" w:sz="4" w:color="B7C9D6"/><w:right w:val="single" w:sz="4" w:color="B7C9D6"/><w:insideH w:val="single" w:sz="4" w:color="D7E0E7"/><w:insideV w:val="single" w:sz="4" w:color="D7E0E7"/></w:tblBorders></w:tblPr>'
    $body = [System.Text.StringBuilder]::new()
    for ($rowIndex = 0; $rowIndex -lt $parsedRows.Count; $rowIndex++) {
        [void]$body.Append('<w:tr>')
        for ($colIndex = 0; $colIndex -lt $columnCount; $colIndex++) {
            $cellText = ''
            if ($colIndex -lt $parsedRows[$rowIndex].Count) { $cellText = $parsedRows[$rowIndex][$colIndex] }
            $shading = ''
            if ($rowIndex -eq 0) { $shading = '<w:shd w:fill="DCEAF3"/><w:b/>' }
            $runs = Inline-Runs $cellText
            $cell = '<w:tc><w:tcPr><w:tcW w:w="3000" w:type="dxa"/>'
            if ($rowIndex -eq 0) { $cell += '<w:shd w:fill="DCEAF3"/>' }
            $cell += '</w:tcPr><w:p><w:pPr><w:pStyle w:val="TableText"/></w:pPr>'
            if ($rowIndex -eq 0) { $cell += '<w:r><w:rPr><w:b/></w:rPr><w:t xml:space="preserve">' + (Escape-Xml $cellText) + '</w:t></w:r>' }
            else { $cell += $runs }
            $cell += '</w:p></w:tc>'
            [void]$body.Append($cell)
        }
        [void]$body.Append('</w:tr>')
    }
    return '<w:tbl>' + $tableProperties + $grid + $body.ToString() + '</w:tbl>'
}

$body = [System.Text.StringBuilder]::new()
$inCode = $false
$inTable = $false
$tableRows = [System.Collections.Generic.List[string]]::new()
$firstHeading = $true
$coverSubtitlePending = $false

for ($i = 0; $i -lt $markdownLines.Length; $i++) {
    $line = $markdownLines[$i]

    if ($line.StartsWith('```')) {
        if ($inTable) {
            [void]$body.Append((New-Table $tableRows.ToArray()))
            $tableRows.Clear(); $inTable = $false
        }
        $inCode = -not $inCode
        continue
    }
    if ($inCode) {
        [void]$body.Append((New-CodeParagraph $line))
        continue
    }

    if ($line.Trim().StartsWith('|')) {
        $inTable = $true
        $tableRows.Add($line)
        continue
    }
    elseif ($inTable) {
        [void]$body.Append((New-Table $tableRows.ToArray()))
        $tableRows.Clear(); $inTable = $false
    }

    if ([string]::IsNullOrWhiteSpace($line) -or $line.Trim() -eq '---') { continue }

    if ($line.StartsWith('# ')) {
        $style = 'Title'
        if (-not $firstHeading) { $style = 'Heading1' }
        [void]$body.Append((New-Paragraph $line.Substring(2).Trim() $style))
        if ($style -eq 'Title') { $coverSubtitlePending = $true }
        $firstHeading = $false
        continue
    }
    if ($line.StartsWith('## ')) {
        $heading = $line.Substring(3).Trim()
        $style = 'Heading1'
        if ($coverSubtitlePending) { $style = 'Subtitle'; $coverSubtitlePending = $false }
        if ($heading -eq 'How to use this guide') {
            [void]$body.Append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
        }
        [void]$body.Append((New-Paragraph $heading $style))
        continue
    }
    if ($line.StartsWith('### ')) {
        [void]$body.Append((New-Paragraph $line.Substring(4).Trim() 'Heading2'))
        continue
    }

    if ($line -match '^\s*-\s+') {
        $item = [regex]::Replace($line, '^\s*-\s+', '').Trim()
        [void]$body.Append((New-Paragraph (([string][char]0x2022) + '  ' + $item) 'Normal'))
        continue
    }
    if ($line -match '^\s*\d+\.\s+') {
        [void]$body.Append((New-Paragraph ($line.Trim()) 'Normal'))
        continue
    }

    [void]$body.Append((New-Paragraph $line.Trim() 'Normal'))
}
if ($inTable) { [void]$body.Append((New-Table $tableRows.ToArray())) }

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><w:body>$($body.ToString())<w:sectPr><w:headerReference w:type="default" r:id="rId2"/><w:footerReference w:type="default" r:id="rId3"/><w:pgSz w:w="12240" w:h="15840"/><w:pgMar w:top="1200" w:right="1080" w:bottom="1200" w:left="1080" w:header="540" w:footer="540" w:gutter="0"/></w:sectPr></w:body></w:document>
"@

$stylesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Aptos" w:hAnsi="Aptos"/><w:sz w:val="21"/><w:color w:val="243746"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="120" w:line="276" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults>
<w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
<w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:before="2400" w:after="240"/></w:pPr><w:rPr><w:rFonts w:ascii="Aptos Display" w:hAnsi="Aptos Display"/><w:b/><w:sz w:val="40"/><w:color w:val="123B5D"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Heading1"><w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="300" w:after="140"/><w:outlineLvl w:val="0"/></w:pPr><w:rPr><w:rFonts w:ascii="Aptos Display" w:hAnsi="Aptos Display"/><w:b/><w:sz w:val="30"/><w:color w:val="123B5D"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Heading2"><w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:pPr><w:keepNext/><w:spacing w:before="220" w:after="100"/><w:outlineLvl w:val="1"/></w:pPr><w:rPr><w:b/><w:sz w:val="24"/><w:color w:val="176B87"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Subtitle"><w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:rPr><w:sz w:val="25"/><w:color w:val="176B87"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="CodeBlock"><w:name w:val="Code Block"/><w:basedOn w:val="Normal"/><w:pPr><w:shd w:fill="F2F5F7"/><w:ind w:left="180" w:right="180"/><w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/></w:pPr><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:sz w:val="18"/><w:color w:val="263746"/></w:rPr></w:style>
<w:style w:type="character" w:styleId="CodeChar"><w:name w:val="Inline Code"/><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas"/><w:shd w:fill="F2F5F7"/><w:sz w:val="19"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="TableText"><w:name w:val="Table Text"/><w:basedOn w:val="Normal"/><w:pPr><w:spacing w:after="40" w:line="240"/></w:pPr><w:rPr><w:sz w:val="18"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Header"><w:name w:val="header"/><w:basedOn w:val="Normal"/><w:pPr><w:jc w:val="right"/></w:pPr><w:rPr><w:sz w:val="16"/><w:color w:val="657987"/></w:rPr></w:style>
<w:style w:type="paragraph" w:styleId="Footer"><w:name w:val="footer"/><w:basedOn w:val="Normal"/><w:pPr><w:jc w:val="center"/></w:pPr><w:rPr><w:sz w:val="16"/><w:color w:val="657987"/></w:rPr></w:style>
</w:styles>
'@

$headerXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:pStyle w:val="Header"/></w:pPr><w:r><w:t>IT Asset Management - Refactor Guide</w:t></w:r></w:p></w:hdr>
'@
$footerXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:pStyle w:val="Footer"/></w:pPr><w:r><w:t>Learning guide - Page </w:t></w:r><w:r><w:fldChar w:fldCharType="begin"/></w:r><w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p></w:ftr>
'@

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/><Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/><Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/></Types>
'@
$packageRels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>
'@
$documentRels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/></Relationships>
'@

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fileStream = [System.IO.File]::Open($outputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
$archive = [System.IO.Compression.ZipArchive]::new($fileStream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
try {
    $parts = @{
        '[Content_Types].xml' = $contentTypes
        '_rels/.rels' = $packageRels
        'word/document.xml' = $documentXml
        'word/styles.xml' = $stylesXml
        'word/header1.xml' = $headerXml
        'word/footer1.xml' = $footerXml
        'word/_rels/document.xml.rels' = $documentRels
    }
    foreach ($entryName in $parts.Keys) {
        $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $entryStream = $entry.Open()
        $writer = [System.IO.StreamWriter]::new($entryStream, [System.Text.UTF8Encoding]::new($false))
        $writer.Write($parts[$entryName])
        $writer.Dispose()
    }
}
finally {
    $archive.Dispose()
    $fileStream.Dispose()
}

Write-Output "Created $outputPath"
