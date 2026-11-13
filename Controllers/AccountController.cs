using System.Security.Claims;
using BTL_TrainTicketApp.Data;
using BTL_TrainTicketApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTL_TrainTicketApp.Controllers;

public sealed class AccountController(TrainTicketDbContext db) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(input);
        }

        var user = await db.Users.FirstOrDefaultAsync(account => account.Email == input.Email);
        if (user is null || user.Status != "HoatDong" || !PasswordMatches(user, input.Password))
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(input);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return user.Role == "Admin"
            ? RedirectToAction("Dashboard", "Admin")
            : user.Role == "NhanVien"
                ? RedirectToAction("Lookup", "Staff")
                : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterInput input)
    {
        if (await db.Users.AnyAsync(user => user.Email == input.Email))
        {
            ModelState.AddModelError(nameof(input.Email), "Email này đã được đăng ký.");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var user = new DbUser
        {
            Name = input.Name.Trim(),
            Email = input.Email.Trim(),
            Phone = input.Phone.Trim(),
            Cccd = input.Cccd.Trim(),
            Role = "KhachHang",
            Status = "HoatDong",
            Created = DateTime.Now
        };
        user.Password = new PasswordHasher<DbUser>().HashPassword(user, input.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();
        TempData["Success"] = "Tạo tài khoản thành công. Hãy đăng nhập để tiếp tục.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Profile()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return View(await db.Users.FindAsync(id));
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string name, string phone, string cccd)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await db.Users.FindAsync(id);
        if (user is null || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(cccd))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
            return RedirectToAction(nameof(Profile));
        }

        user.Name = name.Trim();
        user.Phone = phone.Trim();
        user.Cccd = cccd.Trim();
        await db.SaveChangesAsync();
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                },
                CookieAuthenticationDefaults.AuthenticationScheme)));
        TempData["Success"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Denied() => View();

    private static bool PasswordMatches(DbUser user, string password)
    {
        if (user.Password == password)
        {
            return true;
        }

        if (!user.Password.StartsWith("AQAAAA", StringComparison.Ordinal))
        {
            return false;
        }

        return new PasswordHasher<DbUser>()
            .VerifyHashedPassword(user, user.Password, password) != PasswordVerificationResult.Failed;
    }
[HttpPost]
[Microsoft.AspNetCore.Authorization.Authorize]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
{
    if (!ModelState.IsValid)
    {
        TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Dữ liệu không hợp lệ.";
        return RedirectToAction(nameof(Profile));
    }

    var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var user = await db.Users.FindAsync(id);
    if (user is null) return NotFound();

    if (!PasswordMatches(user, input.CurrentPassword))
    {
        TempData["Error"] = "Mật khẩu hiện tại không chính xác.";
        return RedirectToAction(nameof(Profile));
    }

    user.Password = new PasswordHasher<DbUser>().HashPassword(user, input.NewPassword);
    await db.SaveChangesAsync();

    TempData["Success"] = "Đổi mật khẩu thành công.";
    return RedirectToAction(nameof(Profile));
}

}
