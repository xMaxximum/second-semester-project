using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;
using System.Web;

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

            await _js.InvokeVoidAsync("blazorSetCookie", $"{key}={encodedValue}{expires}; path=/");
        }

        public async Task<string> GetValue(string key, string def = "")
        {
            var cookie = await _js.InvokeAsync<string>("blazorGetCookie", key);
            return string.IsNullOrEmpty(cookie) ? def : Uri.UnescapeDataString(cookie);
        }
    }
}
