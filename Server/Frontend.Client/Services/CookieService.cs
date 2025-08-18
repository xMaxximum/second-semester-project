using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace Frontend.Client.Services
{
    public interface ICookie
    {
        Task SetValue(string key, string value, int? days = null);
        Task<string> GetValue(string key, string def = "");
    }

    public class Cookie : ICookie
    {
        private readonly IJSRuntime _js;

        public Cookie(IJSRuntime jsRuntime)
        {
            _js = jsRuntime;
        }

        public async Task SetValue(string key, string value, int? days = null)
        {
            string encodedValue = Uri.EscapeDataString(value);

            string expires = "";
            if (days.HasValue && days.Value > 0)
            {
                var expireDate = DateTime.UtcNow.AddDays(days.Value);
                expires = $"; expires={expireDate:R}";
            }

            // Use native JS directly via IJSRuntime
            string jsCode = $"document.cookie = '{key}={encodedValue}{expires}; path=/'";
            await _js.InvokeVoidAsync("eval", jsCode);
        }

        public async Task<string> GetValue(string key, string def = "")
        {
            // Use native JS to read cookie
            string jsCode = $@"
                (function() {{
                    const nameEQ = '{key}' + '=';
                    const ca = document.cookie.split(';');
                    for(let i=0;i < ca.length;i++) {{
                        let c = ca[i].trim();
                        if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length,c.length);
                    }}
                    return '';
                }})()";

            var cookie = await _js.InvokeAsync<string>("eval", jsCode);
            return string.IsNullOrEmpty(cookie) ? def : Uri.UnescapeDataString(cookie);
        }
    }
}
