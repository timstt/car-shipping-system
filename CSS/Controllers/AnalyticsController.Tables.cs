using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Linq;
using ASTCUSA.Models;

namespace ASTCUSA.Controllers
{
    // Спільні помічники для керування таблицями бази astcusa_analytics (ЛР4)
    public partial class AnalyticsController
    {
        // Зберегти зміни; якщо база відхилила запис (обмеження, зв'язки) — показати повідомлення замість винятку
        private bool TrySave(string error)
        {
            try
            {
                AnalyticsDb.SaveChanges();
                return true;
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", error);
                return false;
            }
        }

        // Порожній рядок → NULL, інакше без пробілів по краях
        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // Користувачі певної ролі для списків вибору (клієнт, менеджер, логіст)
        private IList<KeyValuePair<int, string>> UsersByRole(UserRole role)
        {
            string name = role.ToString();
            return AnalyticsDb.Users.Where(u => u.Role == name).OrderBy(u => u.Name).ToList()
                .Select(u => new KeyValuePair<int, string>(u.Id, u.Name + " (" + u.Email + ")")).ToList();
        }

        private bool UserHasRole(int? userId, UserRole role)
        {
            if (!userId.HasValue) return false;
            string name = role.ToString();
            return AnalyticsDb.Users.Any(u => u.Id == userId.Value && u.Role == name);
        }
    }
}
