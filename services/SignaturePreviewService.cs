using System.Text.Json;
using FirmadorPades.Models;
using iText.Kernel.Pdf;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using PdfRectangle = iText.Kernel.Geom.Rectangle;

namespace FirmadorPades.Services;

/// <summary>
/// Encapsula el visor temporal, el overlay y la conversión entre pantalla y PDF.
/// El PDF de entrada nunca se modifica en este servicio.
/// </summary>
public sealed class SignaturePreviewService : IDisposable
{
    public const float PreviewWidth = 170f;
    public const float PreviewHeight = 60f;

    private readonly byte[] _pdfBytes;
    private readonly Dictionary<int, PdfRectangle> _pageSizes;
    private readonly byte[] _previewStampBytes;
    private string? _temporaryDirectory;
    private const string PreviewHost = "firmador-preview.local";
    private int _activePage = 1;

    public int PageCount => _pageSizes.Count;
    public SignatureLocation Location { get; private set; }

    public SignaturePreviewService(byte[] pdfBytes, byte[] previewStampBytes)
    {
        _pdfBytes = pdfBytes ?? throw new ArgumentNullException(nameof(pdfBytes));
        _previewStampBytes = previewStampBytes ?? throw new ArgumentNullException(nameof(previewStampBytes));
        _pageSizes = ReadPageSizes(pdfBytes);
        if (_pageSizes.Count == 0) throw new InvalidOperationException("El PDF no contiene páginas.");
        Location = CreateDefaultLocation();
    }

    public async Task LoadAsync(WebView2 viewer, bool selectionMode)
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"FirmadorPades-preview-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        string pdfPath = Path.Combine(_temporaryDirectory, "documento.pdf");
        await File.WriteAllBytesAsync(pdfPath, _pdfBytes);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "pdfjs", "pdf.mjs"), Path.Combine(_temporaryDirectory, "pdf.mjs"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "assets", "pdfjs", "pdf.worker.mjs"), Path.Combine(_temporaryDirectory, "pdf.worker.mjs"));

        string htmlPath = Path.Combine(_temporaryDirectory, "visor.html");
        await File.WriteAllTextAsync(htmlPath, BuildViewerHtml(selectionMode));

        // Los módulos ES y el worker de PDF.js no pueden cargar de forma fiable
        // recursos file:// en WebView2. El host virtual les da un origen HTTPS común.
        viewer.CoreWebView2.SetVirtualHostNameToFolderMapping(
            PreviewHost,
            _temporaryDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        viewer.Source = new Uri($"https://{PreviewHost}/visor.html");
    }

    public async Task SetActivePageAsync(WebView2 viewer, int page)
    {
        _activePage = Math.Clamp(page, 1, PageCount);
        await viewer.CoreWebView2.ExecuteScriptAsync($"goToPage({_activePage});");
    }

    public Task SetSelectionModeAsync(WebView2 viewer, bool enabled) =>
        viewer.CoreWebView2.ExecuteScriptAsync($"setSelectionMode({enabled.ToString().ToLowerInvariant()});");

    /// <summary>Recibe un clic relativo al overlay y lo transforma a puntos PDF.</summary>
    public SignatureLocation SetLocationFromScreen(int pageNumber, float relativeX, float relativeY)
    {
        _activePage = Math.Clamp(pageNumber, 1, PageCount);
        PdfRectangle page = _pageSizes[_activePage];
        float x = Math.Clamp(relativeX, 0f, 1f) * page.GetWidth() + page.GetLeft();
        // HTML cuenta desde arriba; PDF cuenta desde abajo.
        float y = (1f - Math.Clamp(relativeY, 0f, 1f)) * page.GetHeight() + page.GetBottom() - PreviewHeight;

        Location = new SignatureLocation
        {
            Page = _activePage,
            X = Math.Clamp(x, page.GetLeft(), page.GetRight() - PreviewWidth),
            Y = Math.Clamp(y, page.GetBottom(), page.GetTop() - PreviewHeight),
            Width = PreviewWidth,
            Height = PreviewHeight
        };
        return Location;
    }

    public Task RefreshOverlayAsync(WebView2 viewer) =>
        viewer.CoreWebView2.ExecuteScriptAsync($"updateSignature({JsonSerializer.Serialize(Location)});");

    public Task ChangeZoomAsync(WebView2 viewer, bool increase) =>
        viewer.CoreWebView2.ExecuteScriptAsync(increase ? "changeZoom(.15);" : "changeZoom(-.15);");

    private SignatureLocation CreateDefaultLocation()
    {
        PdfRectangle first = _pageSizes[1];
        return new SignatureLocation
        {
            Page = 1,
            X = first.GetRight() - PreviewWidth - 20f,
            Y = first.GetTop() - PreviewHeight - 20f,
            Width = PreviewWidth,
            Height = PreviewHeight
        };
    }

    private string BuildViewerHtml(bool selectionMode)
    {
        string stamp = Convert.ToBase64String(_previewStampBytes);
        string mode = selectionMode ? "true" : "false";
        return $$"""
<!doctype html><html><head><style>
html,body{margin:0;background:#525659}#pages{padding:18px;display:flex;flex-direction:column;align-items:center;gap:18px}.page{position:relative;background:white;box-shadow:0 2px 8px #222}.page.selecting{cursor:crosshair}.signature{display:none;position:absolute;object-fit:fill;opacity:.82;pointer-events:none;filter:drop-shadow(0 1px 2px #333)}
</style></head><body><main id="pages"></main>
<script type="module">import * as pdfjsLib from './pdf.mjs';pdfjsLib.GlobalWorkerOptions.workerSrc='./pdf.worker.mjs';const pages=document.getElementById('pages');let pdf,zoom=1.25,selecting={{mode}},current={{JsonSerializer.Serialize(Location)}};window.setSelectionMode=v=>{selecting=v;document.querySelectorAll('.page').forEach(p=>p.classList.toggle('selecting',v))};window.goToPage=p=>document.getElementById('page-'+p)?.scrollIntoView({behavior:'smooth',block:'center'});window.changeZoom=delta=>{zoom=Math.max(.5,Math.min(2.5,zoom+delta));render()};window.updateSignature=l=>{current=l;document.querySelectorAll('.signature').forEach(s=>s.remove());if(!l)return;const page=document.getElementById('page-'+l.Page);if(!page)return;const img=document.createElement('img');img.className='signature';img.src='data:image/png;base64,{{stamp}}';const w=+page.dataset.pdfWidth,h=+page.dataset.pdfHeight,left=+page.dataset.pdfLeft,bottom=+page.dataset.pdfBottom;img.style.display='block';img.style.left=(((l.X-left)/w)*100)+'%';img.style.top=((1-((l.Y+l.Height-bottom)/h))*100)+'%';img.style.width=((l.Width/w)*100)+'%';img.style.height=((l.Height/h)*100)+'%';page.append(img)};async function render(){pages.replaceChildren();for(let n=1;n<=pdf.numPages;n++){const p=await pdf.getPage(n),v=p.getViewport({scale:zoom}),holder=document.createElement('section'),canvas=document.createElement('canvas');holder.className='page';holder.id='page-'+n;holder.dataset.pdfLeft=p.view[0];holder.dataset.pdfBottom=p.view[1];holder.dataset.pdfWidth=p.view[2]-p.view[0];holder.dataset.pdfHeight=p.view[3]-p.view[1];holder.classList.toggle('selecting',selecting);canvas.width=v.width;canvas.height=v.height;holder.append(canvas);holder.onclick=e=>{if(!selecting)return;const r=holder.getBoundingClientRect();chrome.webview.postMessage(JSON.stringify({page:n,x:(e.clientX-r.left)/r.width,y:(e.clientY-r.top)/r.height}))};pages.append(holder);await p.render({canvasContext:canvas.getContext('2d'),viewport:v}).promise}updateSignature(current)}try{pdf=await pdfjsLib.getDocument('./documento.pdf').promise;await render()}catch(e){chrome.webview.postMessage(JSON.stringify({error:e.message||String(e)}))}</script>
</body></html>
""";
    }

    private static Dictionary<int, PdfRectangle> ReadPageSizes(byte[] pdfBytes)
    {
        using var stream = new MemoryStream(pdfBytes);
        using var document = new PdfDocument(new PdfReader(stream));
        return Enumerable.Range(1, document.GetNumberOfPages()).ToDictionary(p => p, p => document.GetPage(p).GetPageSize());
    }

    public void Dispose()
    {
        if (!string.IsNullOrWhiteSpace(_temporaryDirectory) && Directory.Exists(_temporaryDirectory))
        {
            try { Directory.Delete(_temporaryDirectory, true); } catch { /* El control libera sus archivos al cerrarse. */ }
        }
    }
}
