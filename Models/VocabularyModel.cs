using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TiengAnh.Models
{
    public class VocabularyModel : BaseModel
    {
        [BsonElement("ID_TV")]
        public int ID_TV { get; set; }

        [BsonElement("Word_TV")]
        [Required(ErrorMessage = "Vui lòng nhập từ tiếng Anh")]
        public string Word_TV { get; set; } = null!;

        [BsonElement("Meaning_TV")]
        [Required(ErrorMessage = "Vui lòng nhập nghĩa tiếng Việt")]
        public string Meaning_TV { get; set; } = null!;

        [BsonElement("Example_TV")]
        [Required(ErrorMessage = "Vui lòng nhập câu ví dụ")]
        public string Example_TV { get; set; } = null!;

        [BsonElement("Audio_TV")]
        public string? Audio_TV { get; set; }

        [BsonElement("Image_TV")]
        [Display(Name = "Hình ảnh minh họa")]
        public string? Image_TV { get; set; }

        [BsonElement("Level_TV")]
        [Required(ErrorMessage = "Vui lòng chọn cấp độ")]
        public string Level_TV { get; set; } = null!;

        [BsonElement("ID_CD")]
        [Required(ErrorMessage = "Vui lòng chọn chủ đề")]
        public int ID_CD { get; set; }

        [BsonElement("ID_LT")]
        [Required(ErrorMessage = "Vui lòng chọn loại từ")]
        public string ID_LT { get; set; } = null!;

        [BsonElement("PartOfSpeech")]
        public string? PartOfSpeech { get; set; }

        [BsonElement("TopicName")]
        [Required(ErrorMessage = "Vui lòng chọn chủ đề")]
        public string TopicName { get; set; } = null!;

        [BsonElement("IsFavorite")]
        public bool IsFavorite { get; set; }

        [BsonElement("FavoriteByUsers")]
        public List<string> FavoriteByUsers { get; set; } = new List<string>();

        public bool IsFavoriteByUser(string userId)
        {
            return FavoriteByUsers != null && FavoriteByUsers.Contains(userId);
        }
    }
}