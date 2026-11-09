/* Minimal .xlsx writer for the Class Schedule template (no dependencies).
 *
 * Why it exists: the bundled SheetJS (community build) cannot style cells or make Excel "tables",
 * so the template could not look like the schedule sheet (blue header, banded rows, filter buttons).
 * This writes the few XML parts Excel needs and zips them (store, no compression).
 *
 *   var bytes = XlsxTable.build([{
 *       name: 'Class Schedule',            // sheet name
 *       tableName: 'ClassSchedule',        // Excel table name (letters/digits/_ only, unique)
 *       header: ['Day', ...],              // header text = table column names
 *       rows: [['Monday', ..., 0.75]],     // strings, numbers, or { t: 0.75 } for a time-of-day cell
 *       widths: [12, 8, ...]               // column widths
 *   }]);                                   // -> Uint8Array
 *   XlsxTable.download(bytes, 'file.xlsx');
 */
var XlsxTable = (function() {
    'use strict';

    var NS = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
    var REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
    var XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n';

    // Office palette (accent 1 = 5B9BD5) so the "Table Style Medium 2" is the same blue as the sample sheet.
    var THEME = '<a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Office Theme"><a:themeElements><a:clrScheme name="Office"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="1F497D"/></a:dk2><a:lt2><a:srgbClr val="EEECE1"/></a:lt2><a:accent1><a:srgbClr val="5B9BD5"/></a:accent1><a:accent2><a:srgbClr val="ED7D31"/></a:accent2><a:accent3><a:srgbClr val="A5A5A5"/></a:accent3><a:accent4><a:srgbClr val="FFC000"/></a:accent4><a:accent5><a:srgbClr val="4472C4"/></a:accent5><a:accent6><a:srgbClr val="70AD47"/></a:accent6><a:hlink><a:srgbClr val="0000FF"/></a:hlink><a:folHlink><a:srgbClr val="800080"/></a:folHlink></a:clrScheme><a:fontScheme name="Office"><a:majorFont><a:latin typeface="Cambria"/><a:ea typeface=""/><a:cs typeface=""/><a:font script="Jpan" typeface="&#xFF2D;&#xFF33; &#xFF30;&#x30B4;&#x30B7;&#x30C3;&#x30AF;"/><a:font script="Hang" typeface="&#xB9D1;&#xC740; &#xACE0;&#xB515;"/><a:font script="Hans" typeface="&#x5B8B;&#x4F53;"/><a:font script="Hant" typeface="&#x65B0;&#x7D30;&#x660E;&#x9AD4;"/><a:font script="Arab" typeface="Times New Roman"/><a:font script="Hebr" typeface="Times New Roman"/><a:font script="Thai" typeface="Tahoma"/><a:font script="Ethi" typeface="Nyala"/><a:font script="Beng" typeface="Vrinda"/><a:font script="Gujr" typeface="Shruti"/><a:font script="Khmr" typeface="MoolBoran"/><a:font script="Knda" typeface="Tunga"/><a:font script="Guru" typeface="Raavi"/><a:font script="Cans" typeface="Euphemia"/><a:font script="Cher" typeface="Plantagenet Cherokee"/><a:font script="Yiii" typeface="Microsoft Yi Baiti"/><a:font script="Tibt" typeface="Microsoft Himalaya"/><a:font script="Thaa" typeface="MV Boli"/><a:font script="Deva" typeface="Mangal"/><a:font script="Telu" typeface="Gautami"/><a:font script="Taml" typeface="Latha"/><a:font script="Syrc" typeface="Estrangelo Edessa"/><a:font script="Orya" typeface="Kalinga"/><a:font script="Mlym" typeface="Kartika"/><a:font script="Laoo" typeface="DokChampa"/><a:font script="Sinh" typeface="Iskoola Pota"/><a:font script="Mong" typeface="Mongolian Baiti"/><a:font script="Viet" typeface="Times New Roman"/><a:font script="Uigh" typeface="Microsoft Uighur"/></a:majorFont><a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/><a:font script="Jpan" typeface="&#xFF2D;&#xFF33; &#xFF30;&#x30B4;&#x30B7;&#x30C3;&#x30AF;"/><a:font script="Hang" typeface="&#xB9D1;&#xC740; &#xACE0;&#xB515;"/><a:font script="Hans" typeface="&#x5B8B;&#x4F53;"/><a:font script="Hant" typeface="&#x65B0;&#x7D30;&#x660E;&#x9AD4;"/><a:font script="Arab" typeface="Arial"/><a:font script="Hebr" typeface="Arial"/><a:font script="Thai" typeface="Tahoma"/><a:font script="Ethi" typeface="Nyala"/><a:font script="Beng" typeface="Vrinda"/><a:font script="Gujr" typeface="Shruti"/><a:font script="Khmr" typeface="DaunPenh"/><a:font script="Knda" typeface="Tunga"/><a:font script="Guru" typeface="Raavi"/><a:font script="Cans" typeface="Euphemia"/><a:font script="Cher" typeface="Plantagenet Cherokee"/><a:font script="Yiii" typeface="Microsoft Yi Baiti"/><a:font script="Tibt" typeface="Microsoft Himalaya"/><a:font script="Thaa" typeface="MV Boli"/><a:font script="Deva" typeface="Mangal"/><a:font script="Telu" typeface="Gautami"/><a:font script="Taml" typeface="Latha"/><a:font script="Syrc" typeface="Estrangelo Edessa"/><a:font script="Orya" typeface="Kalinga"/><a:font script="Mlym" typeface="Kartika"/><a:font script="Laoo" typeface="DokChampa"/><a:font script="Sinh" typeface="Iskoola Pota"/><a:font script="Mong" typeface="Mongolian Baiti"/><a:font script="Viet" typeface="Arial"/><a:font script="Uigh" typeface="Microsoft Uighur"/></a:minorFont></a:fontScheme><a:fmtScheme name="Office"><a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="50000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="35000"><a:schemeClr val="phClr"><a:tint val="37000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:tint val="15000"/><a:satMod val="350000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="16200000" scaled="1"/></a:gradFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:shade val="51000"/><a:satMod val="130000"/></a:schemeClr></a:gs><a:gs pos="80000"><a:schemeClr val="phClr"><a:shade val="93000"/><a:satMod val="130000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="94000"/><a:satMod val="135000"/></a:schemeClr></a:gs></a:gsLst><a:lin ang="16200000" scaled="0"/></a:gradFill></a:fillStyleLst><a:lnStyleLst><a:ln w="9525" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"><a:shade val="95000"/><a:satMod val="105000"/></a:schemeClr></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="25400" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="38100" cap="flat" cmpd="sng" algn="ctr"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln></a:lnStyleLst><a:effectStyleLst><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="20000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="38000"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="23000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="35000"/></a:srgbClr></a:outerShdw></a:effectLst></a:effectStyle><a:effectStyle><a:effectLst><a:outerShdw blurRad="40000" dist="23000" dir="5400000" rotWithShape="0"><a:srgbClr val="000000"><a:alpha val="35000"/></a:srgbClr></a:outerShdw></a:effectLst><a:scene3d><a:camera prst="orthographicFront"><a:rot lat="0" lon="0" rev="0"/></a:camera><a:lightRig rig="threePt" dir="t"><a:rot lat="0" lon="0" rev="1200000"/></a:lightRig></a:scene3d><a:sp3d><a:bevelT w="63500" h="25400"/></a:sp3d></a:effectStyle></a:effectStyleLst><a:bgFillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="40000"/><a:satMod val="350000"/></a:schemeClr></a:gs><a:gs pos="40000"><a:schemeClr val="phClr"><a:tint val="45000"/><a:shade val="99000"/><a:satMod val="350000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="20000"/><a:satMod val="255000"/></a:schemeClr></a:gs></a:gsLst><a:path path="circle"><a:fillToRect l="50000" t="-80000" r="50000" b="180000"/></a:path></a:gradFill><a:gradFill rotWithShape="1"><a:gsLst><a:gs pos="0"><a:schemeClr val="phClr"><a:tint val="80000"/><a:satMod val="300000"/></a:schemeClr></a:gs><a:gs pos="100000"><a:schemeClr val="phClr"><a:shade val="30000"/><a:satMod val="200000"/></a:schemeClr></a:gs></a:gsLst><a:path path="circle"><a:fillToRect l="50000" t="50000" r="50000" b="50000"/></a:path></a:gradFill></a:bgFillStyleLst></a:fmtScheme></a:themeElements><a:objectDefaults/><a:extraClrSchemeLst/></a:theme>';

    var STYLES = XML + '<styleSheet xmlns="' + NS + '">' +
        '<numFmts count="1"><numFmt numFmtId="164" formatCode="h:mm\\ AM/PM"/></numFmts>' +
        '<fonts count="1"><font><sz val="11"/><color theme="1"/><name val="Calibri"/><family val="2"/><scheme val="minor"/></font></fonts>' +
        '<fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>' +
        '<borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>' +
        '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>' +
        '<cellXfs count="2">' +
        '<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>' +
        '<xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/>' +
        '</cellXfs>' +
        '<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>' +
        '<dxfs count="0"/><tableStyles count="0" defaultTableStyle="TableStyleMedium2" defaultPivotStyle="PivotStyleLight16"/>' +
        '</styleSheet>';

    function esc(s) {
        return String(s == null ? '' : s)
            .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/g, '')
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function colName(i) {                         // 0 -> A, 7 -> H
        var s = '';
        for (i += 1; i > 0; i = Math.floor((i - 1) / 26)) s = String.fromCharCode(65 + (i - 1) % 26) + s;
        return s;
    }

    function textCell(ref, v) {
        var s = String(v == null ? '' : v);
        if (s === '') return '<c r="' + ref + '"/>';
        return '<c r="' + ref + '" t="inlineStr"><is><t xml:space="preserve">' + esc(s) + '</t></is></c>';
    }

    function sheetXml(sh, selected) {
        var cols = sh.header.length;
        var last = colName(cols - 1) + (sh.rows.length + 1);
        var x = XML + '<worksheet xmlns="' + NS + '" xmlns:r="' + REL + '">' +
            '<dimension ref="A1:' + last + '"/>' +
            '<sheetViews><sheetView' + (selected ? ' tabSelected="1"' : '') + ' workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>' +
            '<sheetFormatPr defaultRowHeight="15"/><cols>';
        for (var c = 0; c < cols; c++) {
            x += '<col min="' + (c + 1) + '" max="' + (c + 1) + '" width="' + ((sh.widths && sh.widths[c]) || 14) + '" customWidth="1"/>';
        }
        x += '</cols><sheetData><row r="1">';
        sh.header.forEach(function(h, c) { x += textCell(colName(c) + '1', h); });
        x += '</row>';
        sh.rows.forEach(function(row, r) {
            x += '<row r="' + (r + 2) + '">';
            for (var c = 0; c < cols; c++) {
                var v = row[c], ref = colName(c) + (r + 2);
                if (v !== null && typeof v === 'object' && typeof v.t === 'number') {
                    x += '<c r="' + ref + '" s="1"><v>' + v.t + '</v></c>';
                } else if (typeof v === 'number') {
                    x += '<c r="' + ref + '"><v>' + v + '</v></c>';
                } else {
                    x += textCell(ref, v);
                }
            }
            x += '</row>';
        });
        return x + '</sheetData><pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>' +
            '<tableParts count="1"><tablePart r:id="rId1"/></tableParts></worksheet>';
    }

    function tableXml(sh, id) {
        var ref = 'A1:' + colName(sh.header.length - 1) + (sh.rows.length + 1);
        var x = XML + '<table xmlns="' + NS + '" id="' + id + '" name="' + sh.tableName + '" displayName="' + sh.tableName +
            '" ref="' + ref + '" totalsRowShown="0"><autoFilter ref="' + ref + '"/><tableColumns count="' + sh.header.length + '">';
        sh.header.forEach(function(h, i) { x += '<tableColumn id="' + (i + 1) + '" name="' + esc(h) + '"/>'; });
        return x + '</tableColumns><tableStyleInfo name="TableStyleMedium2" showFirstColumn="0" showLastColumn="0" showRowStripes="1" showColumnStripes="0"/></table>';
    }

    /* ---------- zip (store only) ---------- */
    var crcTable = null;
    function crc32(bytes) {
        if (!crcTable) {
            crcTable = [];
            for (var n = 0; n < 256; n++) {
                var c = n;
                for (var k = 0; k < 8; k++) c = (c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1);
                crcTable[n] = c >>> 0;
            }
        }
        var crc = 0xFFFFFFFF;
        for (var i = 0; i < bytes.length; i++) crc = crcTable[(crc ^ bytes[i]) & 0xFF] ^ (crc >>> 8);
        return (crc ^ 0xFFFFFFFF) >>> 0;
    }

    function utf8(s) {
        if (typeof TextEncoder !== 'undefined') return new TextEncoder().encode(s);
        var e = unescape(encodeURIComponent(s)), a = new Uint8Array(e.length);
        for (var i = 0; i < e.length; i++) a[i] = e.charCodeAt(i);
        return a;
    }

    function zip(files) {                          // files: [{ name, data(string) }]
        var parts = [], central = [], offset = 0;
        function u16(v) { return [v & 255, (v >>> 8) & 255]; }
        function u32(v) { return [v & 255, (v >>> 8) & 255, (v >>> 16) & 255, (v >>> 24) & 255]; }
        files.forEach(function(f) {
            var name = utf8(f.name), data = utf8(f.data), crc = crc32(data);
            var head = [].concat(u32(0x04034B50), u16(20), u16(0x0800), u16(0), u16(0), u16(0x21),
                u32(crc), u32(data.length), u32(data.length), u16(name.length), u16(0));
            parts.push(new Uint8Array(head), name, data);
            central.push({ name: name, crc: crc, size: data.length, offset: offset });
            offset += head.length + name.length + data.length;
        });
        var cdStart = offset, cdSize = 0;
        central.forEach(function(c) {
            var rec = [].concat(u32(0x02014B50), u16(20), u16(20), u16(0x0800), u16(0), u16(0), u16(0x21),
                u32(c.crc), u32(c.size), u32(c.size), u16(c.name.length), u16(0), u16(0), u16(0), u16(0), u32(0), u32(c.offset));
            parts.push(new Uint8Array(rec), c.name);
            cdSize += rec.length + c.name.length;
        });
        parts.push(new Uint8Array([].concat(u32(0x06054B50), u16(0), u16(0), u16(central.length), u16(central.length),
            u32(cdSize), u32(cdStart), u16(0))));
        var total = 0; parts.forEach(function(p) { total += p.length; });
        var out = new Uint8Array(total), pos = 0;
        parts.forEach(function(p) { out.set(p, pos); pos += p.length; });
        return out;
    }

    function build(sheets) {
        var files = [], types = '', wbSheets = '', wbRels = '';
        sheets.forEach(function(sh, i) {
            var n = i + 1;
            types += '<Override PartName="/xl/worksheets/sheet' + n + '.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
                '<Override PartName="/xl/tables/table' + n + '.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml"/>';
            wbSheets += '<sheet name="' + esc(sh.name) + '" sheetId="' + n + '" r:id="rId' + n + '"/>';
            wbRels += '<Relationship Id="rId' + n + '" Type="' + REL + '/worksheet" Target="worksheets/sheet' + n + '.xml"/>';
            files.push({ name: 'xl/worksheets/sheet' + n + '.xml', data: sheetXml(sh, i === 0) });
            files.push({ name: 'xl/worksheets/_rels/sheet' + n + '.xml.rels', data: XML + '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="' + REL + '/table" Target="../tables/table' + n + '.xml"/></Relationships>' });
            files.push({ name: 'xl/tables/table' + n + '.xml', data: tableXml(sh, n) });
        });
        var k = sheets.length;
        wbRels += '<Relationship Id="rId' + (k + 1) + '" Type="' + REL + '/styles" Target="styles.xml"/>' +
            '<Relationship Id="rId' + (k + 2) + '" Type="' + REL + '/theme" Target="theme/theme1.xml"/>';

        var head = [
            { name: '[Content_Types].xml', data: XML + '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/xl/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>' + types + '</Types>' },
            { name: '_rels/.rels', data: XML + '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="' + REL + '/officeDocument" Target="xl/workbook.xml"/></Relationships>' },
            { name: 'xl/workbook.xml', data: XML + '<workbook xmlns="' + NS + '" xmlns:r="' + REL + '"><bookViews><workbookView activeTab="0"/></bookViews><sheets>' + wbSheets + '</sheets></workbook>' },
            { name: 'xl/_rels/workbook.xml.rels', data: XML + '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' + wbRels + '</Relationships>' },
            { name: 'xl/styles.xml', data: STYLES },
            { name: 'xl/theme/theme1.xml', data: XML + THEME }
        ];
        return zip(head.concat(files));
    }

    function download(bytes, filename) {
        var blob = new Blob([bytes], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        if (window.navigator && window.navigator.msSaveOrOpenBlob) { window.navigator.msSaveOrOpenBlob(blob, filename); return; }
        var url = URL.createObjectURL(blob), a = document.createElement('a');
        a.href = url; a.download = filename;
        document.body.appendChild(a); a.click(); document.body.removeChild(a);
        setTimeout(function() { URL.revokeObjectURL(url); }, 1000);
    }

    return { build: build, download: download };
})();

if (typeof module !== 'undefined' && module.exports) module.exports = XlsxTable;
