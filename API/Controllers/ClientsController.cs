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
    public class ClientsController : ControllerBase
    {
        private readonly ApplicationDBContext _context;
        public ClientsController(ApplicationDBContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IEnumerable<Client>> Get()
        {
            return await _context.Clients.ToListAsync();
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var client = await _context.Clients.FirstOrDefaultAsync(x => x.Id == id);
            
            if(client == null) return NotFound();

            return Ok(client);
        }

        [HttpPost]
        public async Task<IActionResult> Create(Client client)
        {
            if(client == null) return BadRequest();

            await _context.Clients.AddAsync(client);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = client.Id }, client);
        }

        [HttpPut]
        public async Task<IActionResult> Update(Client client)
        {
            if (client == null) return BadRequest();
            
            var client1 = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == client.Id);

            if (client1 == null) return NotFound();

            _context.Clients.Update(client);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {

            var client = await _context.Clients.FirstOrDefaultAsync(c =>c.Id == id);

            if(client == null) return NotFound();

            _context.Clients.Remove(client);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
