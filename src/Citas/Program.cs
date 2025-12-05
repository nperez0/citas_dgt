using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Serilog;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/citas-.log", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Iniciando aplicación de citas DGT");
    
    Log.Information("Creando instancia de Playwright...");
    using var playwright = await Playwright.CreateAsync();
    
    Log.Information("Lanzando navegador Chromium en modo headless...");
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    var context = await browser.NewContextAsync();
    var page = await context.NewPageAsync();

    Log.Information("Navegando a la página de citas DGT...");
    await page.GotoAsync("https://sedeclave.dgt.gob.es/WEB_CITE_CONSULTA/paginas/inicio.faces");
    Log.Information("Página cargada correctamente");

    var selCentro = page.Locator("#formselectorCentro\\:j_id_2h");
    var selTipo = page.Locator("#formselectorCentro\\:idTipoTramiteSelector");
    var selArea = page.Locator("#formselectorCentro\\:idAreaSelector");
    var selSubmit = page.Locator("#formselectorCentro\\:j_id_2x");
    var selTramite = page.Locator("#seleccionarTramitea_264");
    var selCita = page.Locator("#formcita\\:seccionCentro");
    var selCalendario = page.Locator("#formcita\\:calendarioJefatura");

    Log.Information("Seleccionando centro (587)...");
    await selCentro.SelectOptionAsync(["587"]);
    await WaitPrimeFacesQueueAsync(page);
    Log.Information("Centro seleccionado");

    Log.Information("Seleccionando área (CYV)...");
    await selArea.SelectOptionAsync(["CYV"]);
    await WaitPrimeFacesQueueAsync(page);
    Log.Information("Área seleccionada");

    Log.Information("Haciendo clic en el botón de envío...");
    await selSubmit.ClickAsync();
    await WaitPrimeFacesQueueAsync(page);
    Log.Information("Formulario enviado");

    Log.Information("Esperando que el selector de trámite se oculte...");
    await selTramite.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    Log.Information("Selector de trámite oculto");

    Log.Information("Ejecutando JavaScript para seleccionar trámite específico...");
    var clickResult = await page.EvaluateAsync<string>(@"() => {
      const el = document.getElementById('seleccionarTramitea_264');
      if (!el) return 'ERROR: Element not found';
      
      // Hacer el elemento visible temporalmente si está oculto
      const originalDisplay = el.style.display;
      if (el.style.display === 'none' || el.offsetParent === null) {
        el.style.display = 'block';
      }
      
      // Disparar el evento click y otros eventos relacionados
      el.click();
      
      // Disparar eventos manualmente por si PrimeFaces los necesita
      el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
      el.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
      el.dispatchEvent(new MouseEvent('click', { bubbles: true }));
      
      // Restaurar display original
      el.style.display = originalDisplay;
      
      return 'SUCCESS: Click ejecutado (elemento estaba oculto)';
    }");
    Log.Information("Resultado del click JavaScript: {ClickResult}", clickResult);
    
    Log.Information("Esperando que la sección de cita sea visible...");
    try
    {
        await selCita.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        Log.Information("Sección de cita visible");
    }
    catch (TimeoutException)
    {
        Log.Warning("Timeout esperando la sección de cita. Tomando screenshot...");
        await page.ScreenshotAsync(new() { Path = "timeout-error.png", FullPage = true });
        
        throw;
    }

    Log.Information("Verificando disponibilidad del calendario...");
    if (await selCalendario.IsVisibleAsync())
    {
        Log.Information("✓ ¡Citas disponibles!");
        await SendMessageAsync("¡Citas disponibles!");
    }
    else
    {
        Log.Information("✗ No hay citas disponibles");
    }
    
    Log.Information("Aplicación finalizada correctamente");
}
catch (Exception ex)
{
    Log.Error(ex, "Error al ejecutar la aplicación de citas");
    
    try
    {
        await SendMessageAsync($"❌ Error al verificar citas: {ex.Message}");
    }
    catch (Exception telegramEx)
    {
        Log.Error(telegramEx, "Error al enviar mensaje de Telegram");
    }
    
    throw;
}
finally
{
    Log.CloseAndFlush();
}

async Task WaitPrimeFacesQueueAsync(IPage page)
{
    await page.WaitForFunctionAsync(
        "() => window.PrimeFaces ? PrimeFaces.ajax.Queue.isEmpty() : true"
    );
}

async Task SendMessageAsync(string text)
{
    var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
    var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

    if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        throw new InvalidOperationException("Faltan TELEGRAM_BOT_TOKEN o TELEGRAM_CHAT_ID.");

    var bot = new TelegramBotClient(token);

    await bot.SendMessage(
        chatId: chatId,
        text: text,
        parseMode: ParseMode.None,
        cancellationToken: CancellationToken.None
    );
}

