using System;
using System.Security.Principal;
using System.Web;
using System.Web.Security;

namespace ASTCUSA.Models
{
    // Авторизаційний cookie FormsAuthentication. У тікеті (UserData) зберігаються Id, роль та ім'я
    // користувача: "<id>|<роль>|<ім'я>". Тікет шифрується й підписується machineKey.
    public static class AuthTicket
    {
        public static HttpCookie CreateCookie(User user)
        {
            var now = DateTime.Now;
            string userData = string.Join("|", user.Id.ToString(), ((int)user.Role).ToString(), user.Name ?? string.Empty);
            var ticket = new FormsAuthenticationTicket(1, user.Email, now, now.Add(FormsAuthentication.Timeout),
                false, userData, FormsAuthentication.FormsCookiePath);

            return new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true,
                Secure = FormsAuthentication.RequireSSL,
                Path = FormsAuthentication.FormsCookiePath,
                Domain = FormsAuthentication.CookieDomain
            };
        }

        private static string[] ReadData(IIdentity identity)
        {
            var forms = identity as FormsIdentity;
            if (forms == null || !forms.IsAuthenticated || forms.Ticket == null || forms.Ticket.UserData == null)
                return null;
            var parts = forms.Ticket.UserData.Split(new[] { '|' }, 3);
            return parts.Length == 3 ? parts : null;
        }

        // Id поточного користувача з тікета або null для гостя
        public static int? GetUserId(this IIdentity identity)
        {
            var data = ReadData(identity);
            int id;
            return data != null && int.TryParse(data[0], out id) ? id : (int?)null;
        }

        // Роль поточного користувача з тікета (None для гостя)
        public static UserRole GetUserRole(this IIdentity identity)
        {
            var data = ReadData(identity);
            int role;
            if (data == null || !int.TryParse(data[1], out role) || !Enum.IsDefined(typeof(UserRole), role))
                return UserRole.None;
            return (UserRole)role;
        }

        public static string GetDisplayName(this IIdentity identity)
        {
            var data = ReadData(identity);
            return data != null ? data[2] : null;
        }
    }
}
