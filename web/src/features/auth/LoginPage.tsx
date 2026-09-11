import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import { useLoginMutation } from "./authApi";
import { setCredentials } from "./authSlice";

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [login, { isLoading, error }] = useLoginMutation();
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    try {
      const result = await login({ email, password }).unwrap();
      dispatch(
        setCredentials({
          token: result.accessToken,
          userId: result.userId,
          email: result.email,
          role: result.role,
          organizationId: result.organizationId,
        })
      );
      navigate("/");
    } catch {
      // error is already captured by RTK Query's `error` state below
    }
  };

  return (
    <div>
      <h1>Log in</h1>
      <form onSubmit={handleSubmit}>
        <div>
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div>
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {error && <p>Invalid email or password.</p>}
        <button type="submit" disabled={isLoading}>
          {isLoading ? "Logging in..." : "Log in"}
        </button>
      </form>
    </div>
  );
}