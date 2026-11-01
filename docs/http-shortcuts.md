# Cài HTTP Shortcuts trên Android

Sau khi cài xong, bạn chỉ cần bấm một icon trên màn hình chính: chụp ảnh → nhập giá → chọn quán → chọn danh mục → ảnh được gửi lên Relifes.

## Trước khi bắt đầu

- API đã chạy ở `https://photo.relifes.net`. Xem [README](../README.md#deploy).
- Bạn đã có API key. Nếu chưa, tạo bằng lệnh `create-user` trong README.
- Cài app [HTTP Shortcuts](https://play.google.com/store/apps/details?id=ch.rmy.android.http_shortcuts). Nên cài từ Google Play: bản F-Droid lấy vị trí kém chính xác và chậm hơn.

## Bước 1: Tạo biến

Mở menu ba chấm trên màn hình chính → **Variables** (Global Variables). Tạo các biến dưới đây, tất cả đều là loại **Static Variable**:

| Tên biến | Giá trị ban đầu | Ghi chú |
|---|---|---|
| `relifes_api_key` | API key của bạn | Bật **Treat value as secret** và **Exclude stored value from exports** |
| `entry_price` | để trống | Script sẽ điền |
| `entry_currency` | để trống | Script sẽ điền |
| `entry_lat` | để trống | Script sẽ điền |
| `entry_lng` | để trống | Script sẽ điền |
| `entry_place_id` | để trống | Script sẽ điền |
| `entry_place_name` | để trống | Script sẽ điền |
| `entry_category` | để trống | Script sẽ điền |
| `entry_taken_at` | để trống | Script sẽ điền |
| `entry_time_zone` | để trống | Script sẽ điền |

## Bước 2: Tạo shortcut

Bấm nút **+** → chọn loại shortcut HTTP thông thường. Đặt tên `Relifes`.

**Basic Request Settings**

- Method: `POST`
- URL: `https://photo.relifes.net/api/entries`

**Request Headers**: thêm một header

- Tên: `X-Api-Key`
- Giá trị: bấm nút **{ }** rồi chọn biến `relifes_api_key`

**Request Body / Parameters**: đặt **Request Body Type** thành **Parameters (form-data)**, rồi thêm các tham số sau.

Tham số đầu tiên là ảnh:

- Key: `image`
- Loại: **Single File**, hoặc tuỳ chọn ảnh từ camera nếu app có
- Nếu app cho chọn nguồn của file, hãy chọn **camera**

> Tài liệu của app xác nhận có hỗ trợ camera, nhưng không ghi rõ tên nút. Nếu không thấy tuỳ chọn này, xem phần [Không thấy tuỳ chọn camera](#không-thấy-tuỳ-chọn-camera).

Các tham số còn lại đều là loại văn bản (text). Giá trị được chèn bằng nút **{ }**, không gõ tay:

| Key | Giá trị |
|---|---|
| `price` | `{entry_price}` |
| `currency` | `{entry_currency}` |
| `lat` | `{entry_lat}` |
| `lng` | `{entry_lng}` |
| `placeId` | `{entry_place_id}` |
| `placeName` | `{entry_place_name}` |
| `category` | `{entry_category}` |
| `takenAt` | `{entry_taken_at}` |
| `timeZone` | `{entry_time_zone}` |

## Bước 3: Thêm script

Vào mục **Scripting** của shortcut.

Dán đoạn sau vào **Run before Execution**. Chỉ cần sửa 4 dòng đầu nếu bạn đổi tên miền, tiền tệ, múi giờ hoặc danh mục.

```js
const API_URL = 'https://photo.relifes.net';
const CURRENCY = 'VND';
const TIME_ZONE = 'Asia/Ho_Chi_Minh';
const CATEGORIES = ['Ăn uống', 'Cà phê', 'Mua sắm', 'Khác'];

const OTHER_PLACE = 'Nhập tên quán khác';
const NO_PLACE = 'Không gắn quán';
const NO_CATEGORY = 'Không phân loại';

function askPrice() {
  while (true) {
    const value = prompt('Giá (' + CURRENCY + '). Để trống nếu không có.');
    if (value === null) {
      abort();
    }

    const price = value.trim();
    if (price === '' || /^[0-9]{1,16}(\.[0-9]{1,2})?$/.test(price)) {
      return price;
    }
    showToast('Chỉ nhập số, ví dụ 45000 hoặc 12.50');
  }
}

function findNearbyPlaces(location) {
  if (location.status !== 'success') {
    return [];
  }

  const result = sendHttpRequest(
    API_URL + '/api/places/nearby?lat=' + location.latitude + '&lng=' + location.longitude,
    { headers: { 'X-Api-Key': getVariable('relifes_api_key') } },
  );
  if (result.status !== 'success') {
    showToast('Không tải được danh sách quán');
    return [];
  }

  return JSON.parse(result.response.body).places;
}

function typePlaceName() {
  const name = prompt('Tên quán. Để trống nếu không gắn quán.');
  return { id: '', name: name === null ? '' : name.trim() };
}

function askPlace(location) {
  const places = findNearbyPlaces(location);
  if (places.length === 0) {
    return typePlaceName();
  }

  // The number keeps two places with the same name apart.
  const labels = places.map((place, index) => (index + 1) + '. ' + place.name + ' (' + place.distanceMeters + ' m)');
  const choice = showSelection(labels.concat([OTHER_PLACE, NO_PLACE]), 'Chọn quán');

  if (choice === OTHER_PLACE) {
    return typePlaceName();
  }
  if (choice === null || choice === NO_PLACE) {
    return { id: '', name: '' };
  }
  return { id: places[labels.indexOf(choice)].id, name: '' };
}

function askCategory() {
  const choice = showSelection(CATEGORIES.concat([NO_CATEGORY]), 'Danh mục');
  return choice === null || choice === NO_CATEGORY ? '' : choice;
}

const price = askPrice();
const location = getLocation();
const place = askPlace(location);
const category = askCategory();

setVariable('entry_price', price);
setVariable('entry_currency', CURRENCY);
setVariable('entry_lat', location.status === 'success' ? String(location.latitude) : '');
setVariable('entry_lng', location.status === 'success' ? String(location.longitude) : '');
setVariable('entry_place_id', place.id);
setVariable('entry_place_name', place.name);
setVariable('entry_category', category);
setVariable('entry_taken_at', new Date().toISOString());
setVariable('entry_time_zone', TIME_ZONE);
```

Dán đoạn sau vào **Run on Success**:

```js
showToast('Đã lưu');
```

Dán đoạn sau vào **Run on Failure**:

```js
const details = response ? 'Lỗi ' + response.statusCode + ': ' + response.body : 'Lỗi mạng: ' + networkError;
showDialog(details, 'Không lưu được');
```

## Bước 4: Thử

1. Lưu shortcut rồi bấm vào nó. Khi được hỏi, cho phép truy cập vị trí chính xác.
2. Chụp một ảnh, nhập giá, chọn quán rồi chọn danh mục.
3. Bạn sẽ thấy thông báo `Đã lưu`, và ảnh xuất hiện trong trang Nhật ký.
4. Giữ lâu shortcut để đặt icon ra màn hình chính.

Ứng dụng luôn hỏi ảnh trước, sau đó mới chạy script. Thứ tự này do app quy định, nên chụp ảnh sẽ luôn là bước đầu tiên.

## Xử lý sự cố

### Không thấy tuỳ chọn camera

Thử lần lượt:

1. Đặt tham số `image` là **Single File**, bấm chạy, rồi xem trình chọn file có mục **Camera** không. Có hay không tuỳ máy.
2. Chụp bằng app Camera trước, rồi bấm **Chia sẻ** → **HTTP Shortcuts** → `Relifes`. Với cách này, phần giá, quán và danh mục vẫn chạy như bình thường.

### Thông báo lỗi khi gửi

| Thông báo | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Lỗi 401` | API key sai hoặc biến `relifes_api_key` trống | Kiểm tra lại biến |
| `Lỗi 400`, có chữ `image` | Ảnh hỏng, hoặc ảnh ở định dạng HEIC | Trong app Camera, tắt chế độ lưu HEIC ("High efficiency") |
| `Lỗi 413` | Ảnh lớn hơn 20 MB | Giảm độ phân giải của camera |
| `Lỗi 502` | Máy chủ không lưu được ảnh lên R2 | Xem log của API |
| `Lỗi mạng` | Mất mạng hoặc máy chủ không chạy | Kiểm tra mạng rồi thử lại |

### Danh sách quán trống

Khi đó script sẽ hỏi tên quán để bạn gõ tay. Điều này là bình thường trong các trường hợp sau:

- Không lấy được vị trí, kể cả khi đã đợi khoảng 20 giây.
- Không có quán nào trong bán kính khoảng 80 m.
- Đã dùng hết lượt gọi Google trong ngày.
- Máy chủ chưa cấu hình Google API key.

### Quán đúng không có trong danh sách

Khi Relifes đã biết ít nhất một quán trong bán kính khoảng 80 m, nó không gọi Google nữa để tiết kiệm chi phí. Vì vậy danh sách có thể thiếu quán. Khi đó chọn **Nhập tên quán khác**. Lần sau đứng gần đó, tên này sẽ có trong danh sách.

### Đổi tiền tệ khi đi du lịch

Sửa 2 dòng đầu trong script, ví dụ `CURRENCY = 'AUD'` và `TIME_ZONE = 'Australia/Sydney'`. Nếu đi lại thường xuyên, hãy nhân bản shortcut và tạo một bản riêng cho mỗi nơi.
