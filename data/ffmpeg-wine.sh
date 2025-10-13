#!/usr/bin/env bash

PORT="$1"
should_exit=0

if [[ -z "$PORT" ]]; then
  echo "No port was provided. Exiting..."
  exit 1
fi

is_port_in_use() {
  PORT=$1
  if [[ "$OSTYPE" == "darwin"* ]]; then
    if netstat -an | grep -q "LISTEN" | grep -q ":$PORT "; then
      return 0
    fi
  else
    if ss -tuln | grep -q ":$PORT "; then
      return 0
    fi
  fi
  return 1
}

check_dependencies() {
  unmet_dependencies=()

  if [[ "$OSTYPE" == "darwin"* ]]; then
    command -v netstat >/dev/null 2>&1 || unmet_dependencies+=("netstat")
  else
    command -v ss >/dev/null 2>&1 || unmet_dependencies+=("ss")
  fi

  command -v ffmpeg >/dev/null 2>&1 || unmet_dependencies+=("ffmpeg")
  command -v pgrep >/dev/null 2>&1 || unmet_dependencies+=("pgrep")
  command -v grep >/dev/null 2>&1 || unmet_dependencies+=("grep")
  command -v ncat >/dev/null 2>&1 || unmet_dependencies+=("ncat")
  command -v wc >/dev/null 2>&1 || unmet_dependencies+=("wc")

  if [[ ${#unmet_dependencies[@]} -gt 0 ]]; then
    echo "Unmet dependencies:"
    printf '%s\n' "${unmet_dependencies[@]}"
    exit 1
  fi
}

kill_orphaned_ncat() {
  # ncat ocasionally gets orphaned, kill it if there aren't two
  # instances of ffmpeg-wine.sh (this one and another running ncat)
  instances_of_ffmpeg_wine=$(pgrep -f "ffmpeg-wine.sh" | grep -v $$ | wc -l)
  if [[ $instances_of_ffmpeg_wine -ne 2 ]]; then
    ncat_pid=$(pgrep -f "ncat -4 -l 127.0.0.1 $PORT")
    if [[ -n "$ncat_pid" ]]; then
      kill "$ncat_pid" 2>/dev/null
    fi
  fi
}

main() {
  check_dependencies
  kill_orphaned_ncat

  if is_port_in_use $PORT; then
    echo "Port $PORT is already in use"
    exit 1
  fi

  echo "Starting daemon on port $PORT. No further logs will be displayed. You can safely close the terminal; the process will continue running in the background."
  ncat -4 -l 127.0.0.1 $PORT -k -c "
    read -r cmd
    if [[ \"\$cmd\" == \"exit\" ]]; then
      kill \$(pgrep -f \"ncat -4 -l 127.0.0.1 $PORT\")
    fi
    if [[ \"\$cmd\" =~ ^ffmpeg[[:space:]] ]]; then
      eval \"\$cmd\" > /dev/null 2>&1
    fi
  " > /dev/null 2>&1
}

main
