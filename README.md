# BuildFit

> 부품도 맞게, 나에게도 맞게.

BuildFit은 아이코다 가격 자료를 바탕으로 부품 호환성과 사용자의 성향을 함께 검사하는 정적 조립 PC 추천 서비스입니다. CPU부터 케이스, 파워서플라이, 조립비까지 포함한 완성 견적만 제공합니다.

## 데이터 우선 구조

화면에 부품이나 견적을 하드코딩하지 않습니다.

- `src/BuildFit.Web/wwwroot/data/products.json`: 부품 제원, 가격 스냅샷, 출처 참조
- `src/BuildFit.Web/wwwroot/data/builds.json`: 검증 대상 완성 조합과 추천 근거
- `src/BuildFit.Web/wwwroot/data/compatibility-rules.json`: 필수 부품과 파워 여유율
- `src/BuildFit.Web/wwwroot/data/recommendation-profiles.json`: 성능·균형·확장 성향별 가중치와 메모리 구성 원칙
- `src/BuildFit.Web/wwwroot/data/source-manifest.json`: 아이코다 원문 URL과 수집 시점
- `data/schemas/`: 각 JSON 계약의 JSON Schema

Blazor 앱은 위 데이터를 엄격 모드로 역직렬화하고, `BuildFit.Core`가 참조 무결성 및 소켓·메모리·폼팩터·크기·전력·전원 단자를 검사합니다. 필수 정보가 없거나 계약이 맞지 않으면 임의 기본값으로 추천하지 않습니다.

## 로컬 실행

```powershell
npm install
npm run css:build
dotnet test BuildFit.slnx -c Release
dotnet run --project src/BuildFit.Web
```

아이코다 구성 페이지의 옵션 가격 증거를 다시 수집하려면 다음을 실행합니다.

```powershell
pwsh scripts/Import-IcodaSnapshot.ps1
```

가격은 스냅샷 시점 기준이며 실제 주문 전 판매 페이지에서 재확인해야 합니다.
