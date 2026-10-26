using HelpDesk.Api.Contracts;
using HelpDesk.Application.Comments.AddComment;
using HelpDesk.Application.Comments.AddInternalNote;
using HelpDesk.Application.Comments.ListComments;
using HelpDesk.Application.Common.Models;
using HelpDesk.Application.Tickets;
using HelpDesk.Application.Tickets.AssignTicket;
using HelpDesk.Application.Tickets.ChangeTicketStatus;
using HelpDesk.Application.Tickets.ClaimTicket;
using HelpDesk.Application.Tickets.CreateTicket;
using HelpDesk.Application.Tickets.GetTicket;
using HelpDesk.Application.Tickets.GetTicketHistory;
using HelpDesk.Application.Tickets.ListTickets;
using HelpDesk.Application.Tickets.UpdateTicket;
using HelpDesk.Domain;
using HelpDesk.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ISender _sender;

    public TicketsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    [ProducesResponseType(typeof(TicketDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketRequest request)
    {
        var result = await _sender.Send(new CreateTicketCommand(request.Title, request.Description, request.Priority, request.CategoryId));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PaginatedList<TicketSummaryDto>>> List(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? unassignedOnly,
        [FromQuery] bool? assignedToMe,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new ListTicketsQuery(status, priority, categoryId, unassignedOnly, assignedToMe, pageNumber, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(Guid id)
    {
        var result = await _sender.Send(new GetTicketQuery(id));
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TicketDto>> Update(Guid id, UpdateTicketRequest request)
    {
        var result = await _sender.Send(new UpdateTicketCommand(id, request.Title, request.Description, request.CategoryId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/claim")]
    [Authorize(Roles = Roles.Agent)]
    public async Task<ActionResult<TicketDto>> Claim(Guid id)
    {
        var result = await _sender.Send(new ClaimTicketCommand(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TicketDto>> Assign(Guid id, AssignTicketRequest request)
    {
        var result = await _sender.Send(new AssignTicketCommand(id, request.AgentId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<TicketDto>> ChangeStatus(Guid id, ChangeStatusRequest request)
    {
        var result = await _sender.Send(new ChangeTicketStatusCommand(id, request.NewStatus));
        return Ok(result);
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyCollection<TicketHistoryDto>>> History(Guid id)
    {
        var result = await _sender.Send(new GetTicketHistoryQuery(id));
        return Ok(result);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyCollection<TicketCommentDto>>> ListComments(Guid id)
    {
        var result = await _sender.Send(new ListCommentsQuery(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType(typeof(TicketCommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketCommentDto>> AddComment(Guid id, AddCommentRequest request)
    {
        var result = await _sender.Send(new AddCommentCommand(id, request.Content));
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:guid}/internal-notes")]
    [Authorize(Roles = $"{Roles.Agent},{Roles.Admin}")]
    [ProducesResponseType(typeof(TicketCommentDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketCommentDto>> AddInternalNote(Guid id, AddCommentRequest request)
    {
        var result = await _sender.Send(new AddInternalNoteCommand(id, request.Content));
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
