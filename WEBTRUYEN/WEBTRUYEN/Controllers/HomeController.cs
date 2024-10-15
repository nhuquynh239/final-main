using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WEBTRUYEN.Data.Users;
using WEBTRUYEN.Data;

using WEBTRUYEN.Areas.Admin.Controllers;
using Microsoft.EntityFrameworkCore;
using WEBTRUYEN.Models;
using Microsoft.AspNetCore.Hosting;

namespace WEBTRUYEN.Controllers
{
    public class HomeController : Controller
    {

        private readonly ApplicationDbContext db;
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment webHostEnvironment)
        {
            db = context;

            _logger = logger;
            _userManager = userManager; // Kh?i t?o _userManager
            _webHostEnvironment = webHostEnvironment;
        }
        public async Task<IActionResult> Watching(int chapterId)
        {
            // L?y th�ng tin ch??ng theo ID
            var chapter = await db.Chapters.FirstOrDefaultAsync(c => c.Id == chapterId);

            if (chapter == null)
            {
                return NotFound();
            }

            // L?y t?t c? c�c ch??ng c?a truy?n
            var allChapters = await db.Chapters
                .Where(c => c.ProductId == chapter.ProductId) // Gi? s? b?n c� ProductId ?? l?c c�c ch??ng theo truy?n
                .OrderBy(c => c.Id)

                .ToListAsync();

            // T�m ch? s? c?a ch??ng hi?n t?i trong danh s�ch
            int currentIndex = allChapters.FindIndex(c => c.Id == chapterId);

            // X�c ??nh ch??ng tr??c v� ch??ng ti?p theo
            chapter.PreviousChapterId = (currentIndex > 0) ? allChapters[currentIndex - 1].Id : (int?)null;
            chapter.NextChapterId = (currentIndex < allChapters.Count - 1) ? allChapters[currentIndex + 1].Id : (int?)null;

            // Truy?n d? li?u v�o ViewModel
            var viewModel = new ProductDetailsViewModel
            {
                Chapter = chapter,
                AllChapters = allChapters // C� th? th�m n?u b?n c?n danh s�ch t?t c? c�c ch??ng
            };

            // L?y ???ng d?n ??y ?? t?i file
            var extension = System.IO.Path.GetExtension(chapter.FilePath).ToLower();
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, chapter.FilePath.TrimStart('~', '/'));

            // Ki?m tra xem file c� t?n t?i kh�ng
            if (System.IO.File.Exists(filePath))
            {
                // G�n thu?c t�nh Content d?a tr�n lo?i file
                if (extension == ".txt")
                {
                    chapter.Content = await System.IO.File.ReadAllTextAsync(filePath);
                }
                else if (extension == ".jpg" || extension == ".png" || extension == ".gif")
                {
                    // N?u l� h�nh ?nh, c� th? l?u URL ?? hi?n th? trong view
                    chapter.Content = $"<img src='{Url.Content(chapter.FilePath)}' alt='Content Image' style='max-width: 100%; height: auto;' />";
                }
                else if (extension == ".pdf")
                {
                    // N?u l� PDF, c� th? l?u URL ?? hi?n th? trong view
                    chapter.Content = $"<iframe src='{Url.Content(chapter.FilePath)}' width='100%' height='600px' style='border: none;'></iframe>";
                }
                else
                {
                    chapter.Content = "??nh d?ng t?p kh�ng ???c h? tr?.";
                }
            }
            else
            {
                chapter.Content = "T?p kh�ng t?n t?i.";
            }

            return View(viewModel); // Truy?n d? li?u chapter t?i view
        }






        public async Task<IActionResult> Index(string searchTerm, List<int> categoryIds)
        {
            var query = db.Products
                .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
                .Include(p => p.Comments)
                .AsQueryable();

            // L?c s?n ph?m theo ti�u ch� t�m ki?m
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm)
                                      || p.Description.Contains(searchTerm));
            }

            // L?c s?n ph?m theo c�c category ?� ch?n
            if (categoryIds != null && categoryIds.Any())
            {
                query = query.Where(p => p.ProductCategories.Any(pc => categoryIds.Contains(pc.CategoryId)));
            }

            var products = await query.ToListAsync();

            // L?y t?t c? c�c Category ?? hi?n th? trong view
            var categories = await db.Categories.ToListAsync();
            ViewBag.Categories = categories; // Truy?n danh s�ch Category t?i View

            return View(products);
        }


        public async Task<IActionResult> Details(int id, int commentPage = 1, int commentPageSize = 5)
        {
            var product = await db.Products
                .Include(p => p.ProductCategories)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Replies)
                .Include(p => p.Chapters) // ??m b?o t?i danh s�ch ch??ng
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            // T?ng s? l??t xem
            product.IncrementViewCount();
            await db.SaveChangesAsync();

            // L?y ID ng??i d�ng hi?n t?i
            var userId = _userManager.GetUserId(User);
            var user = userId != null ? await db.Users.FindAsync(userId) : null;

            // Ki?m tra quy?n truy c?p ?? hi?n th? danh s�ch ch??ng
            List<Chapter> chaptersToDisplay = null;

            if (product.IsPremium) // N?u truy?n l� premium
            {
                if (user != null && user.IsVip)
                {
                    // N?u ng??i d�ng l� VIP, hi?n th? t?t c? c�c ch??ng
                    chaptersToDisplay = product.Chapters.ToList();
                }
                else
                {
                    // N?u kh�ng, kh�ng hi?n th? ch??ng
                    ViewBag.Message = "B?n c?n ??ng k� VIP ?? xem danh s�ch ch??ng.";
                }
            }
            else
            {
                // N?u truy?n kh�ng ph?i l� premium, hi?n th? t?t c? c�c ch??ng
                chaptersToDisplay = product.Chapters.ToList();
            }

            // T�nh ?i?m trung b�nh cho s?n ph?m
            var ratings = await db.Ratings.Where(r => r.ProductId == id).ToListAsync();
            double averageRating = ratings.Any() ? ratings.Average(r => r.Score) : 0;
            ViewBag.AverageRating = averageRating;

            // Ki?m tra xem ng??i d�ng c� theo d�i s?n ph?m kh�ng
            product.IsFollowed = await db.Follows.AnyAsync(f => f.UserId == userId && f.ProductId == id);

            // L?y ?�nh gi� c?a ng??i d�ng hi?n t?i (n?u c�)
            var userRating = await db.Ratings
                .FirstOrDefaultAsync(r => r.ProductId == id && r.UserId == userId);
            ViewBag.UserRating = userRating;

            // Ph�n trang b�nh lu?n
            var totalComments = await db.Comments.CountAsync(c => c.ProductId == id);
            var pagedComments = await db.Comments
                .Where(c => c.ProductId == id)
                .OrderByDescending(c => c.CreatedAt)
                .Skip((commentPage - 1) * commentPageSize)
                .Take(commentPageSize)
                .ToListAsync();

            ViewBag.CommentTotalPages = (int)Math.Ceiling((double)totalComments / commentPageSize);
            ViewBag.CommentCurrentPage = commentPage;

            // Truy?n d? li?u sang ViewModel
            var viewModel = new ProductDetailsViewModel
            {
                Product = product,
                Comments = pagedComments,
                UserRating = userRating,
                Chapters = chaptersToDisplay, // Truy?n danh s�ch ch??ng v�o ViewModel
                User = user // Th�m ng??i d�ng v�o ViewModel
            };

            return View(viewModel);
        }




    }
}
