using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DatingApp.API.DTOs;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DatingApp.API.Data
{
    public class DatingRepository : IDatingRepository
    {
        private readonly DataContext _context;
        public DatingRepository(DataContext context)
        {
            _context = context;
        }
        public void Add<T>(T entity) where T : class
        {
            _context.Add(entity);
        }

        public void Delete<T>(T entity) where T : class
        {
            _context.Remove(entity);
        }

        public async Task<Like> GetLike(int userId, int receipientId)
        {
            return await _context.Likes.FirstOrDefaultAsync(u => u.LikerId == userId && u.LikeeId == receipientId);
        }

        public async Task<Photo> GetMainPhotoForUser(int userId)
        {
            return await _context.Photos.IgnoreQueryFilters().Where(u => u.UserId == userId)
                                        .FirstOrDefaultAsync(p => p.IsMain);
        }

        public async Task<Photo> GetPhoto(int Id)
        {
            var photo = await _context.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == Id);
            return photo;
        }

        public async Task<User> GetUser(int Id, bool isCurrentUser)
        {
            var query = _context.Users.Include(p => p.Photos).AsQueryable();
            
            if(isCurrentUser)
                query = query.IgnoreQueryFilters();

            var user = await query.FirstOrDefaultAsync(u => u.Id == Id);
            return user;
        }

        public async Task<PagedList<User>> GetUsers(UserParams userParams)
        {
            var users = _context.Users.OrderByDescending(u => u.LastActive).AsQueryable();

            users = users.Where(u => u.Id != userParams.UserId);
            users = users.Where(u => u.Gender == userParams.Gender);

            if (userParams.Likees)
            {
                var userLikers = await GetUsersLike(userParams.UserId, userParams.Likers);
                users = users.Where(u => userLikers.Contains(u.Id));
            }

            if (userParams.Likers)
            {
                var userLikees = await GetUsersLike(userParams.UserId, userParams.Likers);
                users = users.Where(u => userLikees.Contains(u.Id));
            }

            if (userParams.MinAge != 18 || userParams.MaxAge != 99)
            {
                var minDob = DateTime.Today.AddYears(-userParams.MaxAge - 1);
                var maxDob = DateTime.Today.AddYears(-userParams.MinAge);

                users = users.Where(u => u.DateOfBirth >= minDob && u.DateOfBirth <= maxDob);
            }

            if (!string.IsNullOrEmpty(userParams.OrderBy))
            {
                switch (userParams.OrderBy)
                {
                    case "created":
                        users = users.OrderByDescending(u => u.Created);
                        break;
                    default:
                        users = users.OrderByDescending(u => u.LastActive);
                        break;
                }
            }

            return await PagedList<User>.CreateAsync(users, userParams.PageNumber, userParams.PageSize);
        }

        private async Task<IEnumerable<int>> GetUsersLike(int id, bool likers)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

            if (likers)
            {
                return user.Liker.Where(u => u.LikeeId == id).Select(i => i.LikerId);
            }
            else
            {
                return user.Likees.Where(u => u.LikerId == id).Select(i => i.LikeeId);
            }
        }

        public async Task<bool> SaveAll()
        {
            return await _context.SaveChangesAsync() > 0;
        }

        public Task<Message> GetMessage(int id)
        {
            return _context.Messages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<PagedList<Message>> GetMessagesForUser(MessageParams messageParams)
        {
            var messages = _context.Messages.AsQueryable();

            switch (messageParams.MessageContainer)
            {
                case "Inbox":
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId && u.RecipientDeleted == false);
                    break;
                case "Outbox":
                    messages = messages.Where(u => u.SenderId == messageParams.UserId && u.SenderDeleted == false);
                    break;
                default:
                    messages = messages.Where(u => u.RecipientId == messageParams.UserId && u.RecipientDeleted == false && u.IsRead == false);
                    break;
            }

            messages = messages.OrderByDescending(m => m.MessageSent);
            return await PagedList<Message>.CreateAsync(messages, messageParams.PageNumber, messageParams.PageSize);
        }

        public async Task<IEnumerable<Message>> GetMessageThread(int userId, int receipientId)
        {
            var messages = await _context.Messages
                                .Where(m => m.RecipientId == userId && m.RecipientDeleted == false && m.SenderId == receipientId
                                || m.RecipientId == receipientId && m.SenderId == userId && m.SenderDeleted == false)
                                .OrderByDescending(m => m.MessageSent).ToListAsync();
            return messages;
        }

        public async Task<List<PhotoForApprovalDto>> GetUnApprovedPhotos()
        {
            var photos = await _context.Photos.Include( u => u.User).IgnoreQueryFilters()
                            .Where(p => p.IsApproved == false)
                            .Select( u => new PhotoForApprovalDto{ 
                                Id = u.Id, 
                                UserId = u.UserId,  
                                UserName = u.User.UserName, 
                                Url = u.Url, 
                                IsApproved = u.IsApproved }).ToListAsync();

            return photos;
        }

        public async Task<Photo> GetNotApprovedPhoto(int photoId)
        {
            var pendingPhoto = await _context.Photos.IgnoreQueryFilters().Where(p =>p.IsApproved == false)
                                              .FirstOrDefaultAsync(p => p.Id == photoId);
            return pendingPhoto;
        }

        public async Task<Photo> RejectPhotos(int photoId)
        {
            var photoToReject = await _context.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == photoId);
            return photoToReject;
        }

    }
}