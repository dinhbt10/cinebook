using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Controllers;

public class CinemasController : Controller
{
    private readonly CineBookDbContext _context;
    private readonly RoomService _rooms;

    public CinemasController(CineBookDbContext context, RoomService rooms)
    {
        _context = context;
        _rooms = rooms;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Cities = await _context.Cinemas
            .Where(c => c.IsActive)
            .Select(c => c.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        return View(await GetCinemasAsync(null));
    }

    public async Task<IActionResult> List(string? city)
    {
        return PartialView("_CinemaList", await GetCinemasAsync(city));
    }

    private async Task<List<CinemaCardViewModel>> GetCinemasAsync(string? city)
    {
        var query = from c in _context.Cinemas
                    where c.IsActive
                    select c;
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(c => c.City == city);

        var cinemas = await query
            .OrderBy(c => c.City).ThenBy(c => c.Name)
            .Select(c => new CinemaDto(c.Id, c.Name, c.City, c.District, c.Address, c.Hotline))
            .ToListAsync();

        var model = new List<CinemaCardViewModel>();
        foreach (var cinema in cinemas)
        {
            var card = new CinemaCardViewModel();
            card.Cinema = cinema;
            card.Rooms = (await _rooms.GetByCinemaAsync(cinema.Id)).ToList();
            model.Add(card);
        }
        return model;
    }
}
