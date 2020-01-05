using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cnf.esb.web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace cnf.esb.web.Controllers
{
    [Authorize]
    public class ConsumerController : Controller
    {
        readonly EsbModelContext _esbModelContext;

        public ConsumerController(EsbModelContext esbModelContext)
        {
            _esbModelContext = esbModelContext;
        }

        public async Task<IActionResult> Index()
        {
            var consumers = from c in _esbModelContext.Consumers
                            select c;

            return View(await consumers.ToListAsync());
        }


        public IActionResult CreateConsumer()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateConsumer(
            [Bind("Name, HostIP")] EsbConsumer newConsumer)
        {
            if (ModelState.IsValid)
            {
                newConsumer.CreatedOn = DateTime.Now;
                newConsumer.ActiveStatus = 1;
                newConsumer.Token = StringHelper.GetRandomString(20);

                _esbModelContext.Add(newConsumer);
                _esbModelContext.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            return View(newConsumer);
        }

        // GET: Admin/EditConsumer/5
        public IActionResult EditConsumer(int id)
        {
            var consumer = _esbModelContext.Consumers.Find(id);
            if (consumer == null)
            {
                return NotFound();
            }
            return View(consumer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditConsumer(int id,
            [Bind("ID, Name, HostIP")] EsbConsumer editConsumer)
        {
            if (id != editConsumer.ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var orginConsumer = _esbModelContext.Consumers.Find(id);
                    if (orginConsumer == null)
                        return NotFound();
                    orginConsumer.Name = editConsumer.Name;
                    orginConsumer.HostIP = editConsumer.HostIP;

                    _esbModelContext.Update(orginConsumer);
                    _esbModelContext.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_esbModelContext.Consumers.Any(m => m.ID == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(editConsumer);
        }

        //GET: Admin/ShowConsumer/5
        public async Task<IActionResult> ShowConsumer(int id)
        {
            var consumer = await _esbModelContext.Consumers.FindAsync(id);
            return View(consumer);
        }

        //GET: Admin/ConfirmConsumer/5?act=3&name=NC
        public IActionResult ConfirmConsumer(int id, string name, int act)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(name))
                return NotFound();

            ConsumerActionModel.ActionEnum action = (ConsumerActionModel.ActionEnum)act;
            string message;
            switch (action)
            {
                case ConsumerActionModel.ActionEnum.Startup:
                    message = $"准备启用客户程序'{name}'，确定吗？";
                    break;
                case ConsumerActionModel.ActionEnum.Disable:
                    message = $"准备禁用客户程序'{name}'，确定吗？";
                    break;
                case ConsumerActionModel.ActionEnum.Delete:
                    message = $"准备删除客户程序'{name}'，确定吗？";
                    break;
                case ConsumerActionModel.ActionEnum.ResetToken:
                    message = $"准备重置客户程序'{name}'的证书，确定吗？";
                    break;
                default:
                    throw new Exception("Wrong arguments");
            }

            ConsumerActionModel viewModel = new ConsumerActionModel()
            {
                Action = action,
                ConsumerID = id,
                ActionDescription = message
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmConsumer(ConsumerActionModel actionModel)
        {
            if (actionModel.ConsumerID == 0)
                return NotFound();

            var consumer = await _esbModelContext.Consumers.FindAsync(actionModel.ConsumerID);
            if (consumer == null)
                return NotFound();

            switch (actionModel.Action)
            {
                case ConsumerActionModel.ActionEnum.Startup:
                    consumer.ActiveStatus = 1;
                    break;
                case ConsumerActionModel.ActionEnum.Disable:
                    consumer.ActiveStatus = 0;
                    break;
                case ConsumerActionModel.ActionEnum.Delete:
                    _esbModelContext.Consumers.Remove(consumer);
                    await _esbModelContext.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                case ConsumerActionModel.ActionEnum.ResetToken:
                    consumer.Token = StringHelper.GetRandomString(20);
                    break;
                default:
                    throw new Exception("Wrong arguments");

            }
            _esbModelContext.Update(consumer);
            await _esbModelContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}