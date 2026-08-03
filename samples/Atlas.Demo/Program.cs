using Atlas.Blazor.DependencyInjection;
using Atlas.Demo;
using Atlas.Demo.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddAtlasWorkspace();
builder.Services.AddAtlasContent<AtlasDemoContent>("atlas-demo-tool");
builder.Services.AddAtlasContent<AtlasDemoContent>("atlas-demo-editor");

await builder.Build().RunAsync();
