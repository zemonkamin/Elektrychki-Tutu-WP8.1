using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Электрички_Туту
{
    public static class Config
    {

        public static string BaseUrl = LoadBaseUrl();

        private static string LoadBaseUrl()
        {
            try
            {
                var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                if (localSettings.Values.ContainsKey("BaseUrl"))
                {
                    string savedUrl = localSettings.Values["BaseUrl"] as string;
                    if (!string.IsNullOrEmpty(savedUrl))
                    {

                        if (!savedUrl.EndsWith("/"))
                        {
                            savedUrl += "/";
                        }
                        return savedUrl;
                    }
                }
            }
            catch
            {

            }

            return "https://qqq.bccst.ru/tu-tu/";
        }
    }
}