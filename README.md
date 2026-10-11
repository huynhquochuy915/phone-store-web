# phone-store-web
## Quy tắc làm việc nhóm
- KHÔNG code trực tiếp trên main.
- Mỗi người làm trên branch của mình: tv1, tv2, tv3, tv4, tv5.
- Trước khi code: git checkout <branch của mình>, rồi git merge main.
- Code xong: git add . / git commit -m "mô tả" / git push origin <branch của mình>.
- Tạo Pull Request vào main, TV1 review và merge.
- Không sửa _Layout.cshtml, Web.config, file DbContext nếu chưa báo TV1.
- Trước khi bắt đầu mỗi buổi code, mỗi người chạy:
git checkout tv2
git merge main
Cuối buổi thì commit và push. Chỉ cần nhớ hai việc này là đủ.
