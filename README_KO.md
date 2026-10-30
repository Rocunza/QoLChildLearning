# QoL: Child Learning

**림월드 1.6 / 1.7** · **필수: Biotech, Harmony** · 제작자: **ZCN**

바닐라 학습 속도·성장점수·성장 티어 구조를 변경하지 않고 아이들의 학습 관리를 편리하게 하는 모드입니다.

## 주요 기능

- 아동 정착민 초상화에 학습 퍼센트 표시, 학습 중 강조 및 글씨 크기 설정
- 바닐라 성장 기즈모 위에 현재 학습욕구를 **원본 게임 아이콘**으로 표시
- 독서 학습욕구 발생 알림과 초상화 독서 아이콘
- 학습 수치·수면 수치·쿨타임에 따라 자동 기상 시도
- 허기 수치가 낮을 때 작업 전환 시 식사 보조(현재 행동·예약 명령·식량 정책 보호)
- 아동별 독서 알림·자동 기상·자동 식사 기즈모 ON/OFF

기본 설정: 학습 **89.9%** 이하 기상 시도, 수면 **50%** 이상, 기상 쿨타임 게임 내 **1시간**, 자동 식사 허기 **55%**. 실제 기능은 전역 설정과 아동별 기즈모 상태에 따라 달라집니다.

## 설치 방법

1. **Harmony**를 활성화하고 **Biotech** DLC를 사용합니다.
2. GitHub **Releases**에서 설치용 ZIP을 받습니다. GitHub의 자동 생성 *Source code* ZIP은 설치용이 아닙니다.
3. 압축 내부의 `QoLChildLearning` 폴더를 게임 `RimWorld/Mods/` 폴더에 넣습니다.
4. 게임 모드 목록에서 **QoL: Child Learning**을 활성화합니다.

배포 폴더에는 1.6·1.7 전용 DLL이 분리되어 있습니다. 서로 교체하지 마세요.

## 소스 구조

- `1.6/QoLChildLearning/` — 1.6 C# 프로젝트 (.NET Framework 4.7.2)
- `1.7/Projects/QoLChildLearning/` — 1.7 C# 프로젝트 (.NET 10)
- `ModContent/` — 공통 About 정보·번역·아이콘

### 로컬 빌드

게임 라이브러리와 Harmony 바이너리는 저장소에 넣지 않았습니다. `1.6/RimWorld.local.props.example` 또는 `1.7/RimWorld17.local.props.example`을 `.example` 없는 이름으로 복사하고 **본인의 게임 설치 경로**를 입력한 뒤 해당 `.csproj`를 Release 빌드하세요.

## 언어

한국어 / English

## Steam 창작마당

**[Steam 창작마당에서 QoL: Child Learning 구독하기](https://steamcommunity.com/sharedfiles/filedetails/?id=3829534633)**  
창작마당 ID: `3829534633`.

## 라이선스

본 프로젝트는 **[MIT 라이선스](LICENSE)**로 배포합니다.  
Copyright (c) 2026 ZCN.

직접 제작한 프로젝트 소스 코드와 원본 리소스에 MIT 라이선스가 적용됩니다. RimWorld, Biotech, Harmony 및 제3자 리소스는 각 권리자의 별도 조건을 따릅니다.
