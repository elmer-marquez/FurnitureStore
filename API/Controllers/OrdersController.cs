using DATA;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHARED.Models;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDBContext _context;

        public OrdersController(ApplicationDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IEnumerable<Order>> Get()
        {
            return await _context.Orders
                .Include(o=>o.OrderDetails)
                .ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await _context.Orders
                .Include(o=>o.OrderDetails)
                .FirstOrDefaultAsync(o=>o.Id == id);

            if(order == null) return NotFound();

            return Ok(order);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Order order)
        {
            if(order == null) return BadRequest("Orden Null");
            if (order.OrderDetails == null) return BadRequest("Order should have at least one details");

            await _context.Orders.AddAsync(order);
            await _context.OrderDetails.AddRangeAsync(order.OrderDetails);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new {id=order.Id}, order);
        }

        [HttpPut]
        public async Task<IActionResult> Update(Order order)
        {
            if (order.Id <= 0) return BadRequest("Order ID is required");
            if (order == null) return BadRequest("Orden Null");
            if(order.OrderDetails == null) return BadRequest("Order should have at least one details");

            var order1 = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if( order1 == null) return NotFound();

            order1.OrderNumber = order.OrderNumber;
            order1.OrderDate = order.OrderDate;
            order1.DeliveryDate = order.DeliveryDate;
            order1.ClientId = order.ClientId;

            _context.OrderDetails.RemoveRange(order1.OrderDetails);
            _context.Orders.Update(order1);
            _context.OrderDetails.AddRange(order.OrderDetails);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(o=>o.OrderDetails)
                .FirstOrDefaultAsync(o=>o.Id == id);

            if (order == null) return NotFound();

            _context.OrderDetails.RemoveRange(order.OrderDetails);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }

    }
}
