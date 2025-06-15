#!/usr/bin/env bash

PORT="$1"
should_exit=0
[[ -z "$PORT" ]] && exit 1 # exit early, otherwise kill_orphaned_ncat could kill other netcat processes

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
  # while most of these commands should be preinstalled on any sane distro,
  # there exists nixos/arch where the users can be... well, users.
  command -v netstat >/dev/null 2>&1 || command -v ss >/dev/null 2>&1 || should_exit=1
  command -v ffmpeg >/dev/null 2>&1 || should_exit=1
  command -v pgrep >/dev/null 2>&1 || should_exit=1
  command -v grep >/dev/null 2>&1 || should_exit=1
  command -v ncat >/dev/null 2>&1 || should_exit=1
  command -v wc >/dev/null 2>&1 || should_exit=1

  if is_port_in_use $PORT; then
    echo "Port $PORT is already in use"
    should_exit=1
  fi
}

kill_orphaned_ncat() {
  # ncat ocasionally gets orphaned, kill it if there aren't two
  # instances of ffmpeg-wine.sh (this one and another running nc)
  instances_of_ffmpeg_wine=$(pgrep -f "ffmpeg-wine.sh" | grep -v $$ | wc -l)
  if [[ $instances_of_ffmpeg_wine -ne 2 ]]; then
    ncat_pid=$(pgrep -f "ncat -4 -l 127.0.0.1 -p $PORT")
    if [[ -n "$ncat_pid" ]]; then
      kill "$ncat_pid" 2>/dev/null
    fi
  fi
}

main() {
  check_dependencies
  kill_orphaned_ncat

  echo "Starting daemon on port $PORT. Will exit immediately if any dependencies are unmet. There will be no logs from here on."
  while [[ $should_exit -eq 0 ]] ; do
    ncat -4 -l 127.0.0.1 -p $PORT -c '
      read -r cmd
      if [[ "$cmd" == "exit" ]]; then
        echo "exit" > /tmp/ncat_server_exit
        exit
      fi
      if [[ "$cmd" =~ ^ffmpeg[[:space:]] ]]; then
        eval "$cmd" > /dev/null 2>&1
      fi
    '

    if [[ -f /tmp/ncat_server_exit ]]; then
      should_exit=1
      rm /tmp/ncat_server_exit
    fi
  done
}

main
