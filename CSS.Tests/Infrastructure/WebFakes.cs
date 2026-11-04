using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using ASTCUSA.Models;

namespace ASTCUSA.Tests.Infrastructure
{
    // Мінімальні підробки HttpContext, щоб викликати дії контролерів без IIS.
    // Користувач задається так само, як у застосунку: FormsIdentity з тікетом "<id>|<роль>|<ім'я>".
    public static class Web
    {
        public static IPrincipal Principal(int id, UserRole role, string name = "Тест")
        {
            var ticket = new FormsAuthenticationTicket(1, "user" + id + "@test.com", DateTime.Now, DateTime.Now.AddHours(1),
                false, id + "|" + (int)role + "|" + name);
            return new GenericPrincipal(new FormsIdentity(ticket), new string[0]);
        }

        public static IPrincipal Anonymous()
        {
            return new GenericPrincipal(new GenericIdentity(""), new string[0]);
        }

        public static FakeHttpContext Http(IPrincipal user)
        {
            // Статичні методи FormsAuthentication (SignOut, Encrypt) звертаються до HttpContext.Current
            HttpContext.Current = new HttpContext(new HttpRequest("", "https://localhost/", ""), new HttpResponse(new StringWriter()));
            HttpContext.Current.User = user;
            return new FakeHttpContext(user);
        }

        // Контролер із підробленим контекстом запиту від імені користувача
        public static T Controller<T>(IPrincipal user) where T : Controller, new()
        {
            var controller = new T();
            var http = Http(user);
            var routeData = new RouteData();
            controller.ControllerContext = new ControllerContext(http, routeData, controller);
            controller.Url = new UrlHelper(new RequestContext(http, routeData), new RouteCollection());
            return controller;
        }

        public static T Controller<T>(int userId, UserRole role) where T : Controller, new()
        {
            return Controller<T>(Principal(userId, role));
        }
    }

    public class FakeHttpContext : HttpContextBase
    {
        private readonly FakeRequest request;
        private readonly FakeResponse response = new FakeResponse();
        private readonly FakeSession session = new FakeSession();
        private readonly IDictionary items = new Hashtable();

        public FakeHttpContext(IPrincipal user)
        {
            User = user;
            request = new FakeRequest(user != null && user.Identity.IsAuthenticated);
        }

        public override IPrincipal User { get; set; }
        public override HttpRequestBase Request { get { return request; } }
        public override HttpResponseBase Response { get { return response; } }
        public override HttpSessionStateBase Session { get { return session; } }
        public override IDictionary Items { get { return items; } }
    }

    public class FakeRequest : HttpRequestBase
    {
        private readonly bool authenticated;
        private readonly NameValueCollection empty = new NameValueCollection();
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();

        public FakeRequest(bool authenticated) { this.authenticated = authenticated; }

        public override bool IsAuthenticated { get { return authenticated; } }
        public override Uri Url { get { return new Uri("https://localhost/"); } }
        public override Uri UrlReferrer { get { return null; } }
        public override string ApplicationPath { get { return "/"; } }
        public override string AppRelativeCurrentExecutionFilePath { get { return "~/"; } }
        public override string PathInfo { get { return ""; } }
        public override NameValueCollection QueryString { get { return empty; } }
        public override NameValueCollection Form { get { return empty; } }
        public override NameValueCollection Headers { get { return empty; } }
        public override NameValueCollection ServerVariables { get { return empty; } }
        public override HttpCookieCollection Cookies { get { return cookies; } }
    }

    public class FakeResponse : HttpResponseBase
    {
        private readonly HttpCookieCollection cookies = new HttpCookieCollection();
        public override HttpCookieCollection Cookies { get { return cookies; } }
        public override string ApplyAppPathModifier(string virtualPath) { return virtualPath; }
    }

    public class FakeSession : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public override object this[string name]
        {
            get { object v; return values.TryGetValue(name, out v) ? v : null; }
            set { values[name] = value; }
        }
        public override void Remove(string name) { values.Remove(name); }
        public override void Clear() { values.Clear(); }
        public override void Abandon() { values.Clear(); }
    }
}
