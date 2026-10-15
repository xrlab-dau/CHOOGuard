#!/bin/zsh
set -e
provider_task_directory="${0:A:h}"
/usr/bin/env python3 "$provider_task_directory/upgrade_helper.py" --apply --open-browser
printf '%s\n' '연결 도우미 활성화 완료. 브라우저 자동 열기 결과는 위 browser_open_requested에 표시됩니다.' '브라우저 화면 주소: http://127.0.0.1:18953/'
