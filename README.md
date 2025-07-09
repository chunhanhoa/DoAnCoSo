# DoAnCoSo (EngMate)
Chờ xíu xiu cho trang load nha :> | Bạn có thể truy cập dự án tại đây nè:  [EngMate](https://doancoso.onrender.com/)

## Giới thiệu

**EngMate** là một ứng dụng web học tiếng Anh toàn diện, được phát triển dựa trên mô hình Web API. Ứng dụng cung cấp nhiều tính năng giúp người học tiếng Anh cải thiện kỹ năng ngôn ngữ một cách hiệu quả thông qua các bài học từ vựng, ngữ pháp, bài tập thực hành và kiểm tra.

Dự án được xây dựng sử dụng **ASP.NET Core Web API** với ngôn ngữ **C#**, tích hợp cơ sở dữ liệu **MongoDB**, còn phần giao diện người dùng được thiết kế riêng biệt bằng **HTML**, **CSS**, **Bootstrap** và **JavaScript**, giao tiếp với server thông qua các API.

## Tính năng chính

- **Học từ vựng**: Danh sách từ vựng phong phú theo chủ đề và trình độ  
- **Học ngữ pháp**: Các bài học ngữ pháp từ cơ bản đến nâng cao (A1-C1)  
- **Bài tập thực hành**: Đa dạng loại bài tập (trắc nghiệm, điền khuyết, sắp xếp từ,...)  
- **Kiểm tra đánh giá**: Các bài kiểm tra để đánh giá năng lực  
- **Theo dõi tiến độ**: Hệ thống theo dõi tiến độ học tập của người dùng  
- **Yêu thích**: Lưu từ vựng và ngữ pháp yêu thích để học lại  

## Công nghệ sử dụng

- **ASP.NET Core Web API**: Phát triển hệ thống backend cung cấp dữ liệu dạng JSON  
- **C#**: Ngôn ngữ lập trình backend  
- **MongoDB**: Cơ sở dữ liệu NoSQL  
- **Bootstrap 5**: Framework CSS cho giao diện đáp ứng  
- **Font Awesome**: Thư viện biểu tượng  
- **JavaScript/jQuery**: Tương tác phía client  
- **AJAX**: Giao tiếp bất đồng bộ với Web API  

## Kiến trúc dự án

- **Models**: Định nghĩa cấu trúc dữ liệu (GrammarModel, VocabularyModel, TestModel,...)  
- **Controllers (API)**: Cung cấp các endpoint RESTful để xử lý và trả dữ liệu JSON  
- **Repositories**: Truy cập và thao tác dữ liệu từ MongoDB  
- **Services**: Xử lý các dịch vụ như kết nối MongoDB, seeding dữ liệu  
- **Frontend (tách biệt)**: Giao diện người dùng tương tác thông qua các Web API  

## Cài đặt

### Yêu cầu

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)  
- [MongoDB](https://www.mongodb.com/try/download/community)  
- Một công cụ phát triển như [Visual Studio](https://visualstudio.microsoft.com/) hoặc [Visual Studio Code](https://code.visualstudio.com/)  

### Hướng dẫn cài đặt

1. **Clone repository:**

   ```bash
   git clone https://github.com/your-username/DoAnCoSo.git
   ```

2. **Mở dự án:**

   Mở file solution `TiengAnh.sln` bằng Visual Studio hoặc mở thư mục dự án bằng Visual Studio Code.

3. **Cấu hình MongoDB:**

   Cập nhật chuỗi kết nối MongoDB trong file `appsettings.json`:
   
   ```json
   "MongoDbSettings": {
     "ConnectionString": "mongodb://localhost:27017",
     "DatabaseName": "TiengAnh"
   }
   ```

4. **Khởi tạo dữ liệu:**

   Ứng dụng sử dụng `DataSeeder` để tạo dữ liệu mẫu khi khởi động lần đầu.

5. **Build và chạy dự án:**

   ```bash
   dotnet build
   dotnet run
   ```

6. **Truy cập API hoặc kết nối frontend:**

   Mở trình duyệt tại: `http://localhost:5000/api/{resource}`  
   (hoặc sử dụng Postman, hoặc kết nối từ giao diện frontend)

## Cấu trúc chính

- **/Controllers**: Chứa các Web API controller trả dữ liệu JSON  
- **/Models**: Định nghĩa cấu trúc dữ liệu  
- **/Repositories**: Lớp truy cập dữ liệu  
- **/Services**: Các dịch vụ như `MongoDbService`, `DataSeeder`  
- **/wwwroot** *(nếu có)*: Tài nguyên tĩnh hoặc mockup frontend (nếu nhúng sẵn)
