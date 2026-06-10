import { useState } from "react";
import { SplitCurtain } from "../../../shared/components/flit/SplitCurtain.js";

type LoginWelcomeProps = {
  onComplete: () => void;
};

export function LoginWelcome({ onComplete }: LoginWelcomeProps) {
  const [open, setOpen] = useState(false);

  return (
    <SplitCurtain
      open={open}
      variant="logo"
      onActivate={() => setOpen(true)}
      onComplete={onComplete}
    />
  );
}
