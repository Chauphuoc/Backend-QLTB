using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EquipManagementAPI.Data;
using EquipManagementAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipManagementAPI.Helpers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class MaintenanceAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string[] _roleNames;

        public MaintenanceAuthorizeAttribute(params string[] roleNames)
        {
            _roleNames = roleNames;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;
            var user = httpContext.User;

            var authHeader = httpContext.Request.Headers["Authorization"].ToString();
            Console.WriteLine("AUTH HEADER: " + authHeader);
            Console.WriteLine($"User.Identity.IsAuthenticated: {user?.Identity?.IsAuthenticated}");
            Console.WriteLine($"Claims count: {user?.Claims?.Count()}");

            if (user == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var userId = user.Claims
                .FirstOrDefault(c => c.Type == "sub" || c.Type.EndsWith("nameidentifier"))
                ?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            Console.WriteLine("UserId from token: " + userId);

            var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

            var userRoles = await (
                from ur in dbContext.userRoles
                join r in dbContext.roles on ur.RoleID equals r.Id
                where ur.UserID == userId && r.isActive == 0
                select r.Name
            ).ToListAsync();

            Console.WriteLine("UserRoles: " + string.Join(",", userRoles));
            Console.WriteLine("Required Roles: " + string.Join(",", _roleNames));

            if (!_roleNames.Any(role => userRoles.Contains(role)))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
