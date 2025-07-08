using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TiengAnh.Models
{
    public class GrammarModel : BaseModel
    {
        [BsonElement("ID_NP")]
        public int ID_NP { get; set; }
        
        [BsonElement("Title_NP")]
        public string Title_NP { get; set; } = null!;
        
        [BsonElement("Description_NP")]
        public string Description_NP { get; set; } = null!;
        
        [BsonElement("Content_NP")]
        public string? Content_NP { get; set; }
        
        [BsonIgnore]
        public string? Content
        {
            get { return Content_NP; }
            set { Content_NP = value; }
        }
        
        [BsonElement("TimeUpload_NP")]
        public DateTime TimeUpload_NP { get; set; }
        
        [BsonElement("ID_CD")]
        public int ID_CD { get; set; }
        
        [BsonElement("TopicName")]
        public string TopicName { get; set; } = null!;
        
        [BsonElement("Level")]
        public string Level { get; set; } = null!;
        
        [BsonElement("VideoUrl_NP")]
        public string? VideoUrl_NP { get; set; }
        
        [BsonElement("Examples")]
        public List<string>? Examples { get; set; }
        
        [BsonElement("ProgressPercentage")]
        public int ProgressPercentage { get; set; }
        
        [BsonIgnore]
        public bool IsFavorite { get; set; }
        
        [BsonElement("FavoriteByUsers")]
        public List<string> FavoriteByUsers { get; set; } = new List<string>();
        
        public bool IsFavoriteByUser(string userId)
        {
            return FavoriteByUsers != null && FavoriteByUsers.Contains(userId);
        }

        [BsonIgnore]
        public string Title 
        { 
            get { return Title_NP; }
            set { Title_NP = value; }
        }

        [BsonIgnore]
        public string Summary 
        { 
            get { return Description_NP; }
            set { Description_NP = value; }
        }

        [BsonIgnore]
        public int ID 
        { 
            get { return ID_NP; }
            set { ID_NP = value; }
        }
    }
}
