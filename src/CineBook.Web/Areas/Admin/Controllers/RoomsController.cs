using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Areas.Admin.Models;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,CinemaManager")]
public class RoomsController : Controller
{
    private readonly CineBookDbContext _context;
    private readonly RoomService _rooms;

    public RoomsController(CineBookDbContext context, RoomService rooms)
    {
        _context = context;
        _rooms = rooms;
    }

    public async Task<IActionResult> Index()
    {
        var model = await BuildAsync(new CreateRoomViewModel());
        model.Form.CinemaId = model.Cinemas.Select(c => (Guid?)c.Cinema.Id).FirstOrDefault();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoomViewModel form)
    {
        if (ModelState.IsValid)
        {
            var request = new CreateRoomRequest(form.CinemaId!.Value, form.Name, form.RoomType,
                form.RowCount!.Value, form.SeatsPerRow!.Value, form.VipFromRow!.Value, form.VipToRow!.Value);
            var result = await _rooms.CreateAsync(request, User.ManagedCinemaId());
            if (result.Succeeded)
                return RedirectToAction("Index");

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        var model = await BuildAsync(form);
        model.OpenForm = true;
        return View("Index", model);
    }

    private async Task<RoomsIndexViewModel> BuildAsync(CreateRoomViewModel form)
    {
        var scope = User.ManagedCinemaId();

        var cinemas = _context.Cinemas.Where(c => c.IsActive);
        if (scope != null)
            cinemas = cinemas.Where(c => c.Id == scope.Value);

        var query = from c in cinemas
                    orderby c.City, c.Name
                    select new CinemaDto(c.Id, c.Name, c.City, c.District, c.Address, c.Hotline);

        var model = new RoomsIndexViewModel();
        model.Form = form;
        foreach (var cinema in await query.ToListAsync())
        {
            var block = new CinemaRoomsViewModel();
            block.Cinema = cinema;
            block.Rooms = (await _rooms.GetByCinemaAsync(cinema.Id)).ToList();
            model.Cinemas.Add(block);
        }

        ViewBag.CinemaOptions = new SelectList(model.Cinemas.Select(c => c.Cinema), "Id", "Name");
        return model;
    }
}
