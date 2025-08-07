using System;
using System.Text;
using System.Net.Http;

namespace Cyclone_ESP32
{
    internal class HTTPClient
    {
        private string url, username, password;
        private int port;
        private string authToken;

        public HTTPClient(string url, int port, string username, string password)
        {
            this.url = url;
            this.port = port;
            this.username = username;
            this.password = password;
        }

        private void Authenticate()
        {
            // Here you would typically make an HTTP request to authenticate and retrieve a token
            // For demonstration purposes, we'll just set a dummy token
            authToken = "dummyAuthToken";
        }

        private void buildPostData() { 
            string jsonPayload = "{\"username\": \"user1\", \"password\": \"pass123\"}";
            StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        }
    }
}
