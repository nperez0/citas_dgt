using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();

await page.GotoAsync("https://sedeclave.dgt.gob.es/WEB_CITE_CONSULTA/paginas/inicio.faces");

var selCentro = page.Locator("#formselectorCentro\\:j_id_2h");
var selTipo = page.Locator("#formselectorCentro\\:idTipoTramiteSelector");
var selArea = page.Locator("#formselectorCentro\\:idAreaSelector");
var selSubmit = page.Locator("#formselectorCentro\\:j_id_2x");
var selTramite = page.Locator("#seleccionarTramitea_264");
var selCita = page.Locator("#formcita\\:seccionCentro");
var selCalendario = page.Locator("#formcita\\:calendarioJefatura");


await selCentro.SelectOptionAsync(["587"]);
await WaitPrimeFacesQueueAsync(page);

await selArea.SelectOptionAsync(["CYV"]);
await WaitPrimeFacesQueueAsync(page);

await selSubmit.ClickAsync();
await WaitPrimeFacesQueueAsync(page);

await selTramite.WaitForAsync(new() { State = WaitForSelectorState.Hidden });

await page.EvaluateAsync(@"() => {
  const el = document.getElementById('seleccionarTramitea_264');
  if (el) el.click();
}");

await selCita.WaitForAsync(new() { State = WaitForSelectorState.Visible });

if (await selCalendario.IsVisibleAsync())
{
    Console.WriteLine("¡Citas disponibles!");
    await SendMessageAsync("¡Citas disponibles!");
}
else
{
    Console.WriteLine("No hay citas disponibles");
    await SendMessageAsync("No hay citas disponibles");
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

